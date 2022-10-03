import { useState, useEffect, useRef } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { HubConnectionBuilder } from '@microsoft/signalr';
import Global from './global';
// import ShowJson from './components/showJson';
import Home from './pages/home';

import Button from 'react-bootstrap/Button';

import './App.css';




function App() {
  const [hub, setHub] = useState(null); // connection
  const [message, setMessage] = useState("Hello, world: ");
  const [messageRX, setMessageRX] = useState("");

  const [result, setResult] = useState("");
  const [isError, setIsError] = useState(true);

  useEffect(() => {
    console.log(Global);
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
          await hub.start();
          console.log('Connected!', hub.state);
          hub.on('onMessage', (user, msg) => {
            setMessageRX(msg);
          });

          hub.on('onAlert', (message) => {
            alert(message)
          })
        }
        catch (e) {
          console.log('Connection failed: ', e);
        }
      }
    }

    connect();

  }, [hub]);


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
    if (hub.state !== 'Connected') return;

    try {
      await hub.send('SendMessage', "someUser", msg + " " + Date.now());
    }
    catch (e) {
      console.log(e);
    }
  }



  return (
    <div className="App">
      <BrowserRouter>
        <Routes>
          <Route path='/' element={<Home hub={hub} />} />
        </Routes>
      </BrowserRouter>

    </div>
  );
}

export default App;
