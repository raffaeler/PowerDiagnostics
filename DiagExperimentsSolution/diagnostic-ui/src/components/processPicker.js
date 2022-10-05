import { useEffect, useState } from 'react';
import { Button, ListGroup, ListGroupItem, Stack } from 'react-bootstrap';
import Global from '../global';
import ProcessItem from './processItem';

export default function ProcessPicker() {
    const [processes, setProcesses] = useState([]);
    const [selectedProcess, setSelectedProcess] = useState({});
    const [isError, setIsError] = useState(false);
    const [errorMessage, setErrorMessage] = useState("");
    const [enableAttach, setEnableAttach] = useState(false);

    async function getProcesses() {
        console.log('fetching');
        var res = await Global.invokeAPI("GET", Global.apiProcesses);
        if (isError) {
            console.log('getProcesses fetch failed')
            setIsError(res.isError);
            setErrorMessage(res.result);
        }
        else {
            setProcesses(res.result);
        }
    }

    useEffect(() => {
        getProcesses();
    }, []);

    const refresh = () => {
        setEnableAttach(false);
        setSelectedProcess({});
        getProcesses();
    }

    const attach = () => {
        // TODO: call webapi to attach the process and receive the event traces
    }

    const itemClicked = (itemId) => {
        setProcesses((old) =>
            old.map((item) => {
                item.active = false;
                if (item.id === itemId) {
                    let newItem = { ...item, active: !item.active };
                    setSelectedProcess(newItem);
                    setEnableAttach(true);
                    return newItem;
                }
                return item;
            })
        );
    }

    return (
        <>
            <div style={{marginBottom: 10, marginLeft:5}}>{selectedProcess?.name?.length > 0
                ? (<div>{selectedProcess.name} ({selectedProcess.id})</div>)
                : (<div>Select a process</div>) }</div>

            <ListGroup as="ul" style={{ display:'inline-block', overflowY:'scroll', height:'250px',  }} >
                {processes.map(function (data, index) {
                    return (
                        <ListGroupItem key={data.id} as="li" onClick={() => itemClicked(data.id)} active={data.active}>
                            <ProcessItem id={data.id} name={data.name} />
                        </ListGroupItem>
                    );
                })}
            </ListGroup>

            {isError ? (<p>{errorMessage}</p>) : (<p />)}

            <Stack direction="horizontal" gap={3}>
                <Button onClick={refresh}>Refresh</Button>
                <Button onClick={attach} variant="info" disabled={!enableAttach}>Attach</Button>
            </Stack>
        </>
    );
}