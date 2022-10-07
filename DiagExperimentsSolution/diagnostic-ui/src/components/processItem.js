import { useEffect, useState } from 'react';
import { Col, Container, Row } from 'react-bootstrap';

export default function ProcessItem(props) {

    return (
        // <div>{props.id} {props.name}</div>
        <Container>
            <Row >
                {/* style={{fontWeight: 'bold'}} */}
                <Col sm={2} ><div style={styles.valueStyle} className="float-end" >{props.id}</div></Col>
                <Col><div style={styles.valueStyle}>{props.name}</div></Col>
            </Row>
        </Container>
    );
}

const styles = {
    valueStyle: {
        className: 'float-end',
        color: 'navy',
        cursor: 'pointer'
    }
}; 