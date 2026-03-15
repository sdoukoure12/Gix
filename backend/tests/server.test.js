// Tests unitaires du serveur Gix
const { describe, it, before, after } = require('node:test');
const assert = require('node:assert/strict');

let server;
let PORT;

before(async () => {
  PORT = process.env.PORT || 3000;
  server = require('../src/index');
  await new Promise((resolve) => {
    if (server.listening) return resolve();
    server.once('listening', resolve);
  });
});

after(async () => {
  await new Promise((resolve, reject) => server.close((err) => (err ? reject(err) : resolve())));
});

describe('health endpoint', () => {
  it('répond avec status ok', async () => {
    const response = await fetch(`http://localhost:${PORT}/health`);
    const body = await response.json();

    assert.equal(response.status, 200);
    assert.deepEqual(body, { status: 'ok' });
  });
});
