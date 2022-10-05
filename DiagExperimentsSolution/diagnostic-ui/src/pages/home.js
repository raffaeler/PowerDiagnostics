import { useState, useEffect, useRef } from 'react';
import { BrowserRouter, Route } from 'react-router-dom';
import { Link } from "react-router-dom";
import { HubConnectionBuilder, JsonHubProtocol } from '@microsoft/signalr';
import ShowJson from '../components/showJson';
import Global from '../global';
import Button from 'react-bootstrap/Button';


function Home(props) {
    const [message, setMessage] = useState("Hello, world: ");
    const [messageRX, setMessageRX] = useState("");

    const [result, setResult] = useState("");
    const [isError, setIsError] = useState(true);

    useEffect(() => {
        if (props.hub == null) return;

        props.hub.on('onMessage', (user, msg) => {
            setMessageRX(msg);
        });

        props.hub.on('onAlert', (message) => {
            alert(message)
        })

    }, [props.hub]);

    const invokeAPI = async () => {
        try {
            const response = await fetch(Global.baseAddress + Global.apiProcess, {
                headers: {
                },
            });

            if (!response.ok) {
                let message = `Fetch failed with HTTP status ${response.status} ${response.statusText}`;
                setResult(message);
                setIsError(true);
                return;
            }

            setResult(await response.json());
            setIsError(false);
        }
        catch (e) {
            console.log(e);
            setResult(e.message);
            setIsError(true);
        }
    }

    const sendMessage = async msg => {
        console.log(props.hub);
        if (props.hub.state !== 'Connected') return;

        try {
            await props.hub.send('SendMessage', "someUser", msg + " " + Date.now());
        }
        catch (e) {
            console.log(e);
        }
    }



    return (
        <>
            <a href="#" onClick={invokeAPI}>Invoke API</a> &nbsp;&nbsp;&nbsp;
            <a href="#" onClick={() => sendMessage('Hi')}>Hub</a> &nbsp;&nbsp;&nbsp;
            <p>{message} ==> {messageRX}</p>

            <div className="apiResult">
                {isError ? result : (<ShowJson label="API result" data={result} />)}
            </div>

            <Button onClick={() => sendMessage('Hello')}>click me</Button>
            <Button onClick={() => props.hub.off('onMessage')}>Stop</Button>
        </>
    );
}

export default Home;