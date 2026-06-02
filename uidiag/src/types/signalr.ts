/** Event payload sent from the server via SignalR `onEvs`. */
export interface EvsEvent {
  cat: string
  val: string
  uom: string
}

/** Known SignalR server→client event names. */
export type SignalRServerEvent =
  | 'onEvs'
  | 'onMessage'
  | 'onAlert'

/** Known SignalR client→server method names. */
export type SignalRClientMethod =
  | 'SendMessage'
