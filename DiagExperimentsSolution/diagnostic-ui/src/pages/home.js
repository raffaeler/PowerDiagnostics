import { useState, useEffect, useRef } from 'react';
import { BrowserRouter, Route } from 'react-router-dom';
import { Link } from "react-router-dom";
import { HubConnectionBuilder, JsonHubProtocol } from '@microsoft/signalr';
import Button from 'react-bootstrap/Button';

import ProcessPicker from '../components/processPicker';
import Global from '../global';



function Home(props) {
    const [modalShow, setModalShow] = useState(false);
    const [isAttached, setIsAttached] = useState(false);
    const [selectedProcess, setSelectedProcess] = useState({});

    const onAttach = async (process) => {
        console.log("Selected process:", process);
        setModalShow(false);
        if (isAttached) await detach();
        // TODO: call webapi to attach the process and receive the event traces
        await Global.invokeAPI('POST', Global.apiProcessAttach + '/' + process.id);
        setIsAttached(true);
        setSelectedProcess(process);
    }

    const detach = async (e) => {
        // TODO: call webapi to attach the process and receive the event traces
        console.log('Detaching process', selectedProcess.name, e);
        await Global.invokeAPI('POST', Global.apiProcessDetach);
        setIsAttached(false);
    }

    const showPicker = () => {
        console.log("showPicker");
        setModalShow(true);
    }

    const renderPicker = () => {
        if(!isAttached) return (
            <div>
                Process to monitor: <a href="#" onClick={showPicker}>click to select</a>
                <ProcessPicker show={modalShow} onHide={() => setModalShow(false)} onSelectedProcess={onAttach} />
            </div>
        ); else return(
            <div>
                <a href="#" onClick={(e) => detach(e)}>Stop monitoring {selectedProcess.name}</a>
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
