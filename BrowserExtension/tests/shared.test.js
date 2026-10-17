// Checks the blocking rules in shared.js. Run: node BrowserExtension/tests/shared.test.js
const fs = require('fs'), assert = require('assert');
eval(fs.readFileSync(require('path').join(__dirname, '../shared.js'), 'utf8').replace(/^const /gm, 'var '));
const s = { ...DEFAULTS, lists: { ...DEFAULTS.lists }, schedule: { ...DEFAULTS.schedule } };
assert(isDistracting('www.youtube.com', s));
assert(!isDistracting('notyoutube.com', s));
assert(!isDistracting('amazon.com', s));
s.lists.shopping = true; assert(isDistracting('amazon.com', s));
s.lists.custom = true; s.custom = ['foo.io']; assert(isDistracting('a.foo.io', s));
s.allowOnly = true; assert(isDistracting('example.com', s)); assert(!isDistracting('en.wikipedia.org', s)); assert(!isDistracting('localhost', s));
const sch = { on: true, days: [2], from: '09:00', to: '17:00' };
assert(inSchedule(sch, new Date(2026, 9, 13, 10, 0)));   // Tuesday 10:00
assert(!inSchedule(sch, new Date(2026, 9, 13, 17, 0)));
assert(!inSchedule(sch, new Date(2026, 9, 14, 10, 0)));  // Wednesday
assert(inSchedule({ on: true, days: [2], from: '22:00', to: '02:00' }, new Date(2026, 9, 13, 23, 0)));
assert(!inSchedule({ ...sch, on: false }, new Date(2026, 9, 13, 10, 0)));
assert(blockingNow({ running: true, studying: true, strict: true }, { ...s, block: 'strict', schedule: { on: false } }));
assert(!blockingNow({ running: true, studying: true, strict: false }, { ...s, block: 'strict', schedule: { on: false } }));
assert(!blockingNow(null, { ...s, schedule: { on: false } }));
console.log('shared.js checks pass');
