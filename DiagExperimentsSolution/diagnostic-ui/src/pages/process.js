import { useState } from 'react';
import Button from 'react-bootstrap/Button';
import ProcessPicker from '../components/processPicker';


export default function Process(props) {
    console.log('process page');

    return (
        <>
        <ProcessPicker/>
        </>
    );
}