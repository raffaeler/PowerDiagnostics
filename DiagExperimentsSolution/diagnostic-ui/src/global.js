export default class Global {
    static baseAddress = "https://localhost:7072";
    
    static apiProcesses = "/api/processes";                     // () => returns list of processes
    static apiProcessAttach = "/api/processes/attach";          // /{id} => returns no value
    static apiProcessDetach = "/api/processes/detach";          // () => returns no value
    static apiProcessSnapshot = "/api/processes/snapshot";      // /{id} => returns SessionId
    static apiProcessDump = "/api/processes/dump";              // /{id} => returns SessionId

    static apiSessions = "/api/sessions";                       // () => returns list of sessions (strings)
    static apiSessionsQueries = "/api/sessions/queries";        // () => returns list of queries (strings)
    // /api/sessions/{sessionId}/{query}

    static diagnosticHub = "/diagnosticHub";

    static async invokeAPI(verb, relativeAddress) {
        try {
            const response = await fetch(Global.baseAddress + relativeAddress, {
                method: verb,
                headers: {
                },
            });

            if (!response.ok) {
                let message = `Fetch failed with HTTP status ${response.status} ${response.statusText}`;
                return {
                    isError: true,
                    result: message,
                };
            }

            console.log("response", response);
            // console.log("body", response.body);
            // console.log("headers", response.headers);
            // console.log("content-type", response.headers.get('content-type'));
            
            let res;
            if (response.headers.get('content-type') != null)
                res = await response.json();
            else
                res = ""

            return {
                isError: false,
                result: res,
            };
        }
        catch (e) {
            console.log(e);
            return {
                isError: true,
                result: e.message,
            };
        }
    }

}