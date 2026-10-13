// Turns the game's study log ([{day:"yyyy-MM-dd", subject, minutes, sessions}]) into chart buckets. No DOM, so it can be tested in Node.
const RANGES = {
  days:   { count: 30, label: "Last 30 days" },
  weeks:  { count: 12, label: "Last 12 weeks" },
  months: { count: 12, label: "Last 12 months" },
  years:  { count: 0,  label: "All years" }, // as many as the log covers
};
const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

const parseDay = s => { const [y, m, d] = s.split("-").map(Number); return new Date(y, m - 1, d); };
const dayKey = d => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
const weekStart = d => { const w = new Date(d); w.setDate(w.getDate() - ((w.getDay() + 6) % 7)); return w; }; // Monday

// Returns [{key, label, short (axis label), total, sessions, byTag:{tag:minutes}}], oldest first.
function buckets(entries, range, today = new Date()) {
  today = new Date(today.getFullYear(), today.getMonth(), today.getDate());
  const list = [], index = {};
  const add = (key, label, short = label) => { index[key] = list.length; list.push({ key, label, short, total: 0, sessions: 0, byTag: {} }); };
  const keyOf = d =>
    range === "days" ? dayKey(d) :
    range === "weeks" ? dayKey(weekStart(d)) :
    range === "months" ? `${d.getFullYear()}-${d.getMonth()}` : String(d.getFullYear());

  if (range === "days") for (let i = 29; i >= 0; i--) { const d = new Date(today); d.setDate(d.getDate() - i); add(keyOf(d), `${MONTHS[d.getMonth()]} ${d.getDate()}`, String(d.getDate())); }
  if (range === "weeks") for (let i = 11; i >= 0; i--) { const d = weekStart(today); d.setDate(d.getDate() - 7 * i); add(keyOf(d), `${MONTHS[d.getMonth()]} ${d.getDate()}`); }
  if (range === "months") for (let i = 11; i >= 0; i--) { const d = new Date(today.getFullYear(), today.getMonth() - i, 1); add(keyOf(d), MONTHS[d.getMonth()]); }
  if (range === "years") {
    let first = today.getFullYear();
    for (const e of entries) first = Math.min(first, parseDay(e.day).getFullYear());
    for (let y = first; y <= today.getFullYear(); y++) add(String(y), String(y));
  }
  for (const e of entries) {
    const d = parseDay(e.day);
    if (isNaN(d) || d > today) continue;
    const b = list[index[keyOf(d)]];
    if (!b) continue; // outside the range
    b.total += e.minutes;
    b.sessions += e.sessions;
    b.byTag[e.subject] = (b.byTag[e.subject] || 0) + e.minutes;
  }
  return list;
}

// Days the range covers up to today, starting no earlier than the first logged day, for a fair daily average.
function daysCovered(entries, range, today = new Date()) {
  today = new Date(today.getFullYear(), today.getMonth(), today.getDate());
  let start =
    range === "days" ? new Date(today.getFullYear(), today.getMonth(), today.getDate() - 29) :
    range === "weeks" ? (d => (d.setDate(d.getDate() - 77), d))(weekStart(today)) :
    range === "months" ? new Date(today.getFullYear(), today.getMonth() - 11, 1) : null;
  let first = null;
  for (const e of entries) { const d = parseDay(e.day); if (!isNaN(d) && (!first || d < first)) first = d; }
  if (!first) return 1;
  if (!start || first > start) start = first;
  return Math.max(1, Math.round((today - start) / 86400000) + 1);
}

// Tags ordered by all-time minutes, so each keeps its colour whatever range is shown.
function tagOrder(entries) {
  const totals = {};
  for (const e of entries) totals[e.subject] = (totals[e.subject] || 0) + e.minutes;
  return Object.keys(totals).sort((a, b) => totals[b] - totals[a]);
}

if (typeof module !== "undefined") module.exports = { buckets, tagOrder, daysCovered, dayKey };
