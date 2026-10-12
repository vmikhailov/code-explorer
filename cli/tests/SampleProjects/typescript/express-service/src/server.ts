import express from 'express';
const app = express();
app.get('/health', (req, res) => res.send('OK'));
app.listen(8080);
