import { useState, useEffect, useRef } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { Link } from "react-router-dom";

import { HubConnectionBuilder } from '@microsoft/signalr';
import Global from './global';
// import ShowJson from './components/showJson';
import Home from './pages/home';
import Bogus from './pages/bogus';
import Analysis from './pages/analysis';
import Layout from './pages/layout';

import Button from 'react-bootstrap/Button';

import './App.css';


function App() {
  const [hub, setHub] = useState(null); // connection
  const [selectedSessionId, setSelectedSessionId] = useState(null);

  const [message, setMessage] = useState("Hello, world: ");
  const [messageRX, setMessageRX] = useState("");

  const [result, setResult] = useState("");
  const [isError, setIsError] = useState(true);

  useEffect(() => {
    const newConnection = new HubConnectionBuilder()
      .withUrl(Global.baseAddress + Global.diagnosticHub)
      .withAutomaticReconnect()
      .build();

    setHub(newConnection);
  }, []);

  useEffect(() => {
    async function connect() {
      if (hub) {
        try {
          if (hub.state === "Connected") {
            console.log('Already connected to hub');
          }
          else {
            await hub.start();
            console.log('Connection to hub was successful', hub.state);
          }
        }
        catch (e) {
          console.log('Connection to hub failed: ', e);
        }
      }
    }

    connect();
  }, [hub]);

  const onSelectedSessionId = (sessionId) => {
    setSelectedSessionId(sessionId);
  }

  return (
    <div className="App">
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<Layout hub={hub} />}>
            <Route index element={<Home onSelectedSessionId={onSelectedSessionId}/>} />
            {/* <Route path='home' element={<Home hub={hub} />} /> */}
            <Route path='analysis' element={<Analysis hub={hub} sessionId={selectedSessionId} />} />
          </Route>
        </Routes>
      </BrowserRouter>

    </div>
  );
}

export default App;
