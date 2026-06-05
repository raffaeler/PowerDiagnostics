/** Event payload sent from the server via SignalR `onEvs`. */
export interface EvsEvent {
  cat: string
  val: string
  uom: string
  timestamp?: string
}

/** Known SignalR server→client event names. */
export type SignalRServerEvent =
  | 'onEvs'
  | 'onMessage'
  | 'onAlert'
  | 'onGcRootProgress'
  | 'onGcRootComplete'
  | 'onQueryProgress'
  | 'onQueryComplete'
  | 'onSessionCreated'
  | 'onSessionClosed'

/** Known SignalR client→server method names. */
export type SignalRClientMethod =
  | 'SendMessage'
