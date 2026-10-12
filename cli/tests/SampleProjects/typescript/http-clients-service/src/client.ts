import request from 'request';
import undici from 'undici';
import urllib from 'urllib';

export function callRequestService() {
    request.get('https://api.request-service.com/v1/data', (err, res, body) => {
        console.log(body);
    });
}

export async function callUndiciService() {
    const res = await undici.request('https://api.undici-service.com/v1/orders', {
        method: 'GET'
    });
    return res;
}

export async function callUrllibService() {
    const res = await urllib.request('https://api.urllib-service.com/v1/users', {
        method: 'GET'
    });
    return res;
}
