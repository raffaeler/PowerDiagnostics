import { useState, useEffect, useRef } from 'react';
import { BrowserRouter, Route } from 'react-router-dom';
import { Link } from "react-router-dom";
import { HubConnectionBuilder, JsonHubProtocol } from '@microsoft/signalr';

import ShowJson from '../components/showJson';
import Global from '../global';
import Button from 'react-bootstrap/Button';
import ProcessPicker from '../components/processPicker';


function Bogus(props) {
    const [modalShow, setModalShow] = useState(false);
    
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
        return Global.invokeAPI("GET", Global.apiProcesses);

        try {
            const response = await fetch(Global.baseAddress + Global.apiProcesses, {
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
            

            <Button variant="primary" onClick={() => setModalShow(true)}>
                Select a running .NET process
            </Button>
            
            <ProcessPicker show={modalShow} onHide={() => setModalShow(false)} />
        </>
    );
}

export default Bogus;
