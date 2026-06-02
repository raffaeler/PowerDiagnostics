import {
  HubConnectionBuilder,
  HubConnection,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { HUB_PATH } from '@/config'
import type { SignalRServerEvent } from '@/types/signalr'

export type ConnectionStateChangeCallback = (state: HubConnectionState) => void

/**
 * Manages the SignalR HubConnection lifecycle.
 * Plain TypeScript class — usable inside Zustand stores and outside React.
 */
export class SignalRService {
  private _connection: HubConnection | null = null
  private _stateListeners = new Set<ConnectionStateChangeCallback>()
  /** Pending event handlers registered before the connection exists. */
  private _pendingEvents = new Map<string, ((...args: unknown[]) => void)[]>()

  get connection(): HubConnection | null {
    return this._connection
  }

  get state(): HubConnectionState {
    return this._connection?.state ?? HubConnectionState.Disconnected
  }

  /** Start the connection. Idempotent — safe to call multiple times. */
  async start(): Promise<void> {
    if (this._connection) {
      if (this._connection.state === HubConnectionState.Connected) return
      if (this._connection.state === HubConnectionState.Connecting) return
      // Disconnected or Reconnecting — tear down and rebuild
      await this._connection.stop()
    }

    // Relative URL — works through Vite proxy in dev, same-origin in production
    this._connection = new HubConnectionBuilder()
      .withUrl(HUB_PATH)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    this._connection.onreconnecting(() => this._notifyState())
    this._connection.onreconnected(() => this._notifyState())
    this._connection.onclose(() => this._notifyState())

    // Re-register any pending event handlers on the new connection
    this._replayPendingEvents()

    await this._connection.start()
    this._notifyState()
  }

  /** Stop the connection gracefully. */
  async stop(): Promise<void> {
    if (this._connection) {
      await this._connection.stop()
      this._connection = null
    }
    this._notifyState()
  }

  /** Subscribe to a server→client event. Works even if connection hasn't started yet. */
  onEvent(event: SignalRServerEvent, callback: (...args: unknown[]) => void): void {
    // Register on current connection if it exists
    this._connection?.on(event, callback)

    // Store callback so it's re-registered when a new connection is created
    const existing = this._pendingEvents.get(event)
    if (existing) {
      existing.push(callback)
    } else {
      this._pendingEvents.set(event, [callback])
    }
  }

  /** Unsubscribe from a server→client event. */
  offEvent(event: SignalRServerEvent): void {
    this._connection?.off(event)
    this._pendingEvents.delete(event)
  }

  /** Invoke a client→server hub method. */
  async invoke(method: string, ...args: unknown[]): Promise<void> {
    if (this._connection?.state === HubConnectionState.Connected) {
      await this._connection.invoke(method, ...args)
    }
  }

  /** Register a listener for connection state changes. Returns unsubscribe fn. */
  onStateChange(cb: ConnectionStateChangeCallback): () => void {
    this._stateListeners.add(cb)
    return () => this._stateListeners.delete(cb)
  }

  // ── private helpers ──

  private _notifyState(): void {
    const s = this.state
    this._stateListeners.forEach((cb) => cb(s))
  }

  /** Re-register all pending event handlers on the current connection. */
  private _replayPendingEvents(): void {
    for (const [event, callbacks] of this._pendingEvents) {
      for (const cb of callbacks) {
        this._connection?.on(event, cb)
      }
    }
  }
}

/** Singleton instance. */
export const signalRService = new SignalRService()
