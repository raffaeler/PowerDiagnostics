import { useState, useEffect, useRef } from 'react';
import { Link, BrowserRouter, Route } from 'react-router-dom';
import { useNavigate, useLocation } from "react-router-dom";
import { HubConnectionBuilder, JsonHubProtocol } from '@microsoft/signalr';
import Button from 'react-bootstrap/Button';

import ProcessPicker from '../components/processPicker';
import Global from '../global';

function Home(props) {
    const [modalShow, setModalShow] = useState(false);
    const [isAttached, setIsAttached] = useState(false);
    const [selectedProcess, setSelectedProcess] = useState({});
    const [selectedSessionId, setSelectedSessionId] = useState(null);

    const [isError, setIsError] = useState(false);
    const [errorMessage, setErrorMessage] = useState("");

    let navigate = useNavigate();

    const onAttach = async (process) => {
        console.log("Selected process:", process);
        setModalShow(false);
        if (isAttached) await detach();
        // TODO: call webapi to attach the process and receive the event traces
        var res = await Global.invokeAPI('POST', Global.apiProcessAttach + '/' + process.id);
        if(res.isError) {
            alert(res.message);
            return;
        }
        
        setIsAttached(true);
        setSelectedProcess(process);
    }

    const detach = async () => {
        // TODO: call webapi to attach the process and receive the event traces
        console.log('Detaching process', selectedProcess.name);
        var res = await Global.invokeAPI('POST', Global.apiProcessDetach);
        if(res.isError) {
            alert(res.message);
            return;
        }
        
        setIsAttached(false);
        setSelectedProcess({});
    }

    const showPicker = () => {
        console.log("showPicker");
        setModalShow(true);
    }

    const onSelectedSessionId = (id) => {
        console.log('sessionId', id)
        setSelectedSessionId(id);
        props.onSelectedSessionId(id);
    }

    const snapshot = async () => {
        var res = await Global.invokeAPI('POST', Global.apiProcessSnapshot + '/' + selectedProcess.id);
        if(res.isError) {
            alert(res.message);
            return;
        }

        onSelectedSessionId(res.result);    // sessionId
        navigate('/analysis');
    }

    const dump = async () => {
        var res = await Global.invokeAPI('POST', Global.apiProcessDump + '/' + selectedProcess.id);
        if(res.isError) {
            alert(res.message);
            return;
        }

        onSelectedSessionId(res.result);    // sessionId
        navigate('/analysis');
    }

    const renderPicker = () => {
        if(!isAttached) return (
            <div>
                Process to monitor: <a href="#" onClick={showPicker}>click to select</a>
                <ProcessPicker show={modalShow} onHide={() => setModalShow(false)} onSelectedProcess={onAttach} />
            </div>
        ); else return(
            <div>
                <div>
                    <a href="#" onClick={detach}>Stop monitoring {selectedProcess.name}</a>
                </div>
                <div>
                    <p>Create and analyze a <a href="#" onClick={snapshot}>snapshot</a> or a <a href="#" onClick={dump}>dump</a></p>
                </div>
            </div>
        );
    }

    return (
        <>
            {renderPicker()}
        </>
    );
}

export default Home;
