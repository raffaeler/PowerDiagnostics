import { useState, useEffect } from 'react';
import { Badge, Button, Container, Stack } from 'react-bootstrap';
import Form from 'react-bootstrap/Form'
import ShowJson from '../components/showJson';
import Global from '../global';

export default function Analysis(props) {
    console.log('analysis page', props);
    const [isError, setIsError] = useState(false);
    const [errorMessage, setErrorMessage] = useState("");
    const [queries, setQueries] = useState([]);
    const [selectedQuery, setSelectedQuery] = useState("");
    const [queryResult, setQueryResult] = useState({});

    async function getQueries() {
        console.log('fetching queries');
        var res = await Global.invokeAPI("GET", Global.apiSessionsQueries);
        console.log(res);
        if (isError) {
            console.log('getQueries fetch failed')
            setIsError(res.isError);
            setErrorMessage(res.result);
        }
        else {
            setIsError(false);
            setQueries(res.result);
            console.log(res.result);
        }
    }


    useEffect(() => {
        getQueries();
    }, []);


    const onSelectedQuery = (value) => {
        console.log("selected query: ", value);
        setSelectedQuery(value);
    }

    const runQuery = async () => {
        console.log('run query', selectedQuery);
        var res = await Global.invokeAPI('POST', Global.apiSessions + '/' + props.sessionId + '/' + selectedQuery);
        if(res.isError) {
            console.log('runquery failed')
            setIsError(res.isError);
            setErrorMessage(res.result);
            return;
        }

        setIsError(false);
        setQueryResult(res.result);
        console.log('query result', res.result);
    }

    if (props.sessionId) {
        return (
            <>
                <div style={{ width: 320 }}>
                    <Stack direction="horizontal" gap={3}>
                        <div className="bg-light xl">Query:</div>
                        <Form.Group>
                            {/* <Form.Label>Select a query</Form.Label> */}
                            <Form.Select as='select' onChange={(e) => onSelectedQuery(e.target.value)} >
                                <option value='' disabled>Select a query</option>
                                {queries.map(function (data, index) {
                                    return (
                                        <option key={data}>{data}</option>
                                    );
                                })}
                            </Form.Select>
                        </Form.Group>
                        <Button onClick={runQuery}>Run</Button>
                    </Stack>
                </div>
                <ShowJson data={queryResult} />
            </>
        );
    } else {
        return (
            <>
                <div>A snapshot or dump is needed to start an analysis. Select or create one from the home page.</div>
            </>
        );
    }
}