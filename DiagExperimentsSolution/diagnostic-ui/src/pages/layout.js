import { useState, useEffect, useRef } from 'react';
import { Link, Outlet } from 'react-router-dom';
import { Container, Navbar, Nav, Row, Col } from 'react-bootstrap';
import { LinkContainer } from 'react-router-bootstrap';
import EventsBar from '../components/eventsBar';

export default function Layout(props) {

    return (
        <div>
            {/* fixed="top" */}
            <Navbar collapseOnSelect expand="lg" bg="light" variant="light" >
                <Container fluid>
                    <LinkContainer to="/">
                        <Navbar.Brand>PowerDiagnostics</Navbar.Brand>
                    </LinkContainer>
                    <Navbar.Toggle aria-controls="responsive-navbar-nav" />
                    <Navbar.Collapse id="responsive-navbar-nav">
                        <Nav className="mr-auto">

                            {/* Replacement for <Link to="home">Home</Link> {" "} */}
                            {/* <LinkContainer to="/">
                                <Nav.Link>Home</Nav.Link>
                            </LinkContainer> */}

                            {/* Replacement for <Link to="process">Process</Link> {" "} */}
                            <LinkContainer to="analysis">
                                <Nav.Link>Analysis</Nav.Link>
                            </LinkContainer>

                        </Nav>

                        {/*
                            <Nav className="ms-auto">
                                <LinkContainer to="home" >
                                    <Nav.Link>Right link</Nav.Link>
                                </LinkContainer>
                            </Nav>
                        */}
                    </Navbar.Collapse>
                </Container>
            </Navbar>

            <EventsBar hub={props.hub} />

            <div className="content">
                <Outlet />
            </div>
        </div>
    );
}

const styles = {
    // valueStyle: {
    //     textAlign: 'center',
    //     color: 'navy'
    // },

    // headerStyle: {
    //     textAlign: 'center',
    //     fontWeight: 'bold',
    //     color: 'navy'
    // }
};