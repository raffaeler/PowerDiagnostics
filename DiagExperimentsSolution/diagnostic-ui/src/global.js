export default class Global {
    static baseAddress = "https://localhost:7072";
    static apiProcesses = "/api/Processes";
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

            return {
                isError: false,
                result: await response.json(),
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