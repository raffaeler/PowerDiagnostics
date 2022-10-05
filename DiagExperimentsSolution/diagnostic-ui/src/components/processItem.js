import { useEffect, useState } from 'react';

export default function ProcessItem(props) {

    return (
        <div>{props.id} {props.name}</div>
    );
}