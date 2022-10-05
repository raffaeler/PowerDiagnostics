import { useState, useEffect, useRef } from 'react';
import { Link, Outlet } from 'react-router-dom';
import { Container, Navbar, Nav } from 'react-bootstrap';
import { LinkContainer } from 'react-router-bootstrap';

export default function Layout() {
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
                            <LinkContainer to="home">
                                <Nav.Link>Home</Nav.Link>
                            </LinkContainer>

                            {/* Replacement for <Link to="process">Process</Link> {" "} */}
                            <LinkContainer to="process">
                                <Nav.Link>Process</Nav.Link>
                            </LinkContainer>


                        </Nav>
                        <Nav className="ms-auto">
                            <LinkContainer to="home" >
                                <Nav.Link>Test</Nav.Link>
                            </LinkContainer>
                        </Nav>
                    </Navbar.Collapse>
                </Container>
            </Navbar>
            <div className="content">
                <Outlet />
            </div>
        </div>
    );
}