import { useEffect, useState } from 'react';
import { Button, Col, Row, Container, Modal, ListGroup, ListGroupItem, Stack } from 'react-bootstrap';

import Global from '../global';
import ProcessItem from './processItem';

export default function ProcessPicker(props) {
    const [processes, setProcesses] = useState([]);
    const [selectedProcess, setSelectedProcess] = useState({});
    // const [isAttached, setIsAttached] = useState(false);
    const [isError, setIsError] = useState(false);
    const [errorMessage, setErrorMessage] = useState("");
    const [enableAttach, setEnableAttach] = useState(false);
    const [isSortedById, setIsSortedById] = useState(true);
    const [isSortedByName, setIsSortedByName] = useState(false);
    const [isSortedAscending, setIsSortedAscending] = useState(true);

    async function getProcesses() {
        console.log('fetching');
        var res = await Global.invokeAPI("GET", Global.apiProcesses);
        console.log(res);
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

    // const attach = async () => {
    //     if (isAttached) await detach();
    //     // TODO: call webapi to attach the process and receive the event traces
    //     await Global.invokeAPI('POST', Global.apiProcessAttach + '/' + selectedProcess.id);
    //     setIsAttached(true);
    // }

    // const detach = async () => {
    //     // TODO: call webapi to attach the process and receive the event traces
    //     await Global.invokeAPI('POST', Global.apiProcessAttach + '/' + selectedProcess.id);
    //     setIsAttached(false);
    // }

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

    const sortById = () => {
        let newSortAscending;
        if(isSortedById && isSortedAscending)
            newSortAscending = false;
        else
            newSortAscending = true;
        let mul = newSortAscending ? 1 : -1;

        setProcesses((procs) => {
            const clone = [...procs];
            clone.sort((a, b) => mul * (Number(a.id) - Number(b.id)));
            return clone;
          }); 

          setIsSortedById(true);
          setIsSortedByName(false);
          setIsSortedAscending(newSortAscending);
      }

    const sortByName = () => {
        let newSortAscending;
        if(isSortedByName && isSortedAscending)
            newSortAscending = false;
        else
            newSortAscending = true;
        let mul = newSortAscending ? 1 : -1;

        setProcesses((procs) => {
            const clone = [...procs];
            clone.sort((a, b) => mul * (a.name.toLowerCase() >= b.name.toLowerCase() ? 1 : -1));
            return clone;
          });
        setIsSortedByName(true);
        setIsSortedById(false);
        setIsSortedAscending(newSortAscending);
    }

    let {onSelectedProcess, ...modalProps} = props;
    return (
        <Modal {...modalProps} aria-labelledby="contained-modal-title-vcenter" onEntered={() => getProcesses()}>
            <Modal.Header closeButton>
                <Modal.Title id="contained-modal-title-vcenter">Select a process</Modal.Title>
            </Modal.Header>
            <Modal.Body className="show-grid">
                {/* <div style={{marginBottom: 10, marginLeft:5}}>{selectedProcess?.name?.length > 0
                    ? (<div>{selectedProcess.name} ({selectedProcess.id})</div>)
                    : (<div>Select a process</div>) }</div> */}

                <Row>
                    <Col sm={2} onClick={sortById}><div style={styles.headerStyle} className="float-end">Id</div></Col>
                    <Col onClick={sortByName}><div style={styles.headerStyle}>Name</div></Col>
                </Row>             
                <ListGroup as="ul" style={{ display: 'inline-block', overflowX: 'auto', overflowY: 'scroll', height: '250px', width: '100%' }} >
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
                </Stack>
            </Modal.Body>
            <Modal.Footer>
                <Button onClick={refresh}>Refresh</Button>
                <Button onClick={() => props.onSelectedProcess(selectedProcess)} variant="info" disabled={!enableAttach}>Attach</Button>
                {/* <Button onClick={props.onHide}>Close</Button> */}
            </Modal.Footer>
        </Modal>
    );
}

const styles = {
    headerStyle: {
        textAlign: 'center',
        fontWeight: 'bold',
        className: 'float-end',
        color: 'navy',
        cursor: 'pointer'
    }
}; 