import { useState, useEffect, useRef } from 'react';
import { Container, Navbar, Nav, Row, Col } from 'react-bootstrap';
import { LinkContainer } from 'react-router-bootstrap';


export default function EventsBar(props) {
    const [evsCpu, setEvsCpu] = useState("");
    const [evsCustomHeader, setEvsCustomHeader] = useState("");
    const [evsException, setEvsException] = useState(" ");
    const [evsGcAllocation, setEvsGcAllocation] = useState("");
    const [evsHttpRequests, setEvsHttpRequests] = useState("");
    const [evsWorkingSet, setEvsWorkingSet] = useState("");

    useEffect(() => {
        if (props.hub == null) return;

        props.hub.on('onEvs', (evsString) => {
            let evs = JSON.parse(evsString);
            //console.log(evs);
            switch (evs.cat) {
                case "CPU":
                    setEvsCpu(evs);
                    break;
                case "Custom header":
                    setEvsCustomHeader(evs);
                    break;
                case "Last first-chance Exception":
                    setEvsException(evs);
                    break;
                case "Last GC Allocation":
                    setEvsGcAllocation(evs);
                    break;
                case "HTTP Req/s":
                    setEvsHttpRequests(evs);
                    break;
                case "Working set":
                    setEvsWorkingSet(evs);
                    break;
            }
        });
    }, [props.hub]);


    return (
        <div>
            <Nav style={{ background: '#c2cded' }} >
                <Container >
                    <Row >
                        <Nav >
                            <Col lg={1} >
                                <Nav.Link style={styles.headerStyle} onClick={() => setEvsCpu("")}>CPU</Nav.Link>
                            </Col>
                            <Col lg={2}>
                                <Nav.Link style={styles.headerStyle} onClick={() => setEvsGcAllocation("")}>Last GC Alloc</Nav.Link>
                            </Col>
                            <Col lg={2}>
                                <Nav.Link style={styles.headerStyle} onClick={() => setEvsWorkingSet("")}>Working set</Nav.Link>
                            </Col>
                            <Col lg={2}>
                                <Nav.Link style={styles.headerStyle} onClick={() => setEvsHttpRequests("")}>HTTP req/s</Nav.Link>
                            </Col>
                            <Col lg={2}>
                                <Nav.Link style={styles.headerStyle} onClick={() => setEvsCustomHeader("")}>Custom header</Nav.Link>
                            </Col>
                            <Col lg={3}>
                                <Nav.Link style={styles.headerStyle} onClick={() => setEvsException("")}>Last first-chance exception</Nav.Link>
                            </Col>
                        </Nav>
                    </Row>

                    <Row >
                        <Nav >
                            <Col lg={1} >
                                <Nav.Item style={styles.valueStyle} >{evsCpu.val}{evsCpu.uom}</Nav.Item>
                            </Col>
                            <Col lg={2}>
                                <Nav.Item style={styles.valueStyle} >{evsGcAllocation.val}{evsGcAllocation.uom}</Nav.Item>
                            </Col>
                            <Col lg={2}>
                                <Nav.Item style={styles.valueStyle} >{evsWorkingSet.val}{evsWorkingSet.uom}</Nav.Item>
                            </Col>
                            <Col lg={2}>
                                <Nav.Item style={styles.valueStyle} >{evsHttpRequests.val}{evsHttpRequests.uom}</Nav.Item>
                            </Col>
                            <Col lg={2}>
                                <Nav.Item style={styles.valueStyle} >{evsCustomHeader.val}{evsCustomHeader.uom}</Nav.Item>
                            </Col>
                            <Col lg={3}>
                                <Nav.Item style={styles.valueStyle} >{evsException.val}{evsException.uom}</Nav.Item>
                            </Col>
                        </Nav>
                    </Row>

                </Container>
            </Nav>


        </div>
    );
}

const styles = {
    valueStyle: {
        textAlign: 'center',
        color: 'navy'
    },

    headerStyle: {
        textAlign: 'center',
        fontWeight: 'bold',
        color: 'navy'
    }
}; 