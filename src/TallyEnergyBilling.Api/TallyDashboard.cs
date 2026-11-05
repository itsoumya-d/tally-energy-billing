namespace TallyEnergyBilling.Api;

public static class TallyDashboard
{
    public static string Page() => """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Tally Energy Billing</title>
  <style>
    @import url('https://fonts.googleapis.com/css2?family=Nunito+Sans:wght@400;600;700;800&display=swap');
    :root {
      --tally-font-heading: 'Nunito Sans', sans-serif;
      --tally-font-body: 'Nunito Sans', sans-serif;
      --tally-font-mono: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
      --tally-bg: #FFFFFF;
      --tally-surface: #F5F5F5;
      --tally-ink: #2C365D;
      --tally-mint: #4EEECA;
      --tally-radius: 1px;
      --tally-space: 8px;
    }
    * { box-sizing: border-box; }
    body { margin: 0; background: var(--tally-bg); color: var(--tally-ink); font-family: var(--tally-font-body); }
    header { background: var(--tally-ink); color: #fff; padding: calc(var(--tally-space) * 5) calc(var(--tally-space) * 4); }
    .wrap { max-width: 1080px; margin: 0 auto; }
    .eyebrow { margin: 0 0 var(--tally-space); color: var(--tally-mint); font-size: 11px; font-weight: 800; letter-spacing: .14em; text-transform: uppercase; }
    h1 { margin: 0 0 var(--tally-space); font-family: var(--tally-font-heading); font-size: clamp(30px, 5vw, 48px); line-height: 1.05; letter-spacing: -0.02em; }
    header p { margin: 0; max-width: 680px; color: rgba(255,255,255,.75); line-height: 1.6; }
    main { max-width: 1080px; margin: 0 auto; padding: calc(var(--tally-space) * 4); }
    .kpis { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: calc(var(--tally-space) * 2); }
    .card { background: var(--tally-surface); border-radius: var(--tally-radius); padding: calc(var(--tally-space) * 2); }
    .label { margin: 0 0 6px; color: #6b7280; font-size: 10px; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
    .value { margin: 0; font-family: var(--tally-font-heading); font-size: 27px; font-weight: 800; letter-spacing: -0.02em; }
    .value small { font-size: 11px; font-weight: 600; color: #6b7280; }
    .panels { display: grid; grid-template-columns: 1.4fr 1fr; gap: calc(var(--tally-space) * 2); margin-top: calc(var(--tally-space) * 2); }
    .panel { background: var(--tally-bg); border: 1px solid #e8e8e8; border-radius: var(--tally-radius); padding: calc(var(--tally-space) * 2); }
    .panel h2 { margin: 0 0 var(--tally-space); font-family: var(--tally-font-heading); font-size: 18px; }
    table { width: 100%; border-collapse: collapse; font-size: 12px; }
    th { text-align: left; color: #6b7280; font-size: 10px; text-transform: uppercase; letter-spacing: .08em; padding: 6px 4px; border-bottom: 1px solid #e8e8e8; }
    td { padding: 7px 4px; border-bottom: 1px solid #f0f0f0; }
    td.amount { text-align: right; font-family: var(--tally-font-mono); }
    .total { border-top: 2px solid var(--tally-ink); font-weight: 800; color: var(--tally-ink); }
    .mint { color: #1B9C82; }
    .status { font-size: 12px; color: #6b7280; margin-top: var(--tally-space); }
    @media (max-width: 900px) { .kpis { grid-template-columns: repeat(2, 1fr); } .panels { grid-template-columns: 1fr; } }
  </style>
</head>
<body>
<header>
  <div class="wrap">
    <p class="eyebrow">Tally Group · Billing platform</p>
    <h1>Energy Bill Review</h1>
    <p>Time-of-use usage, daily supply, demand, solar export credit and GST for one retail energy account. The service is .NET/ASP.NET Core with SQL Server-shaped invoice models.</p>
  </div>
</header>
<main>
  <section class="kpis">
    <article class="card"><p class="label">Bill total</p><p class="value" id="total">—</p></article>
    <article class="card"><p class="label">Usage</p><p class="value" id="usage">—</p></article>
    <article class="card"><p class="label">Supply</p><p class="value" id="supply">—</p></article>
    <article class="card"><p class="label">Demand</p><p class="value" id="demand">—</p></article>
    <article class="card"><p class="label">Export credit</p><p class="value mint" id="export">—</p></article>
  </section>

  <section class="panels">
    <article class="panel">
      <h2>Invoice lines</h2>
      <table>
        <thead><tr><th>Description</th><th>Quantity</th><th>Unit</th><th>Amount</th></tr></thead>
        <tbody id="lines"><tr><td colspan="4">Loading…</td></tr></tbody>
        <tfoot>
          <tr class="total"><td colspan="3">Subtotal</td><td class="amount" id="subtotal">—</td></tr>
          <tr><td colspan="3">GST 15%</td><td class="amount" id="gst">—</td></tr>
          <tr class="total"><td colspan="3">Total</td><td class="amount" id="total-2">—</td></tr>
        </tfoot>
      </table>
      <p class="status" id="status">Loading sample bill…</p>
    </article>
    <article class="panel">
      <h2>Usage by band</h2>
      <table>
        <thead><tr><th>Band</th><th>kWh</th></tr></thead>
        <tbody id="bands"><tr><td colspan="2">Loading…</td></tr></tbody>
      </table>
      <p class="status">Peak 32c/kWh · Shoulder 22c/kWh · Off-peak 12c/kWh · Supply $1.80/day · Demand $12.50/kW · Export 8c/kWh.</p>
    </article>
  </section>
</main>
<script>
  const money = (cents) => '$' + (cents / 100).toFixed(2);
  fetch('/api/sample-bill').then((r) => r.json()).then((bill) => {
    document.getElementById('total').textContent = money(bill.totalCents);
    document.getElementById('usage').textContent = money(bill.usageCents);
    document.getElementById('supply').textContent = money(bill.supplyCents);
    document.getElementById('demand').textContent = money(bill.demandCents);
    document.getElementById('export').textContent = money(bill.exportCreditCents);
    document.getElementById('subtotal').textContent = money(bill.subtotalCents);
    document.getElementById('gst').textContent = money(bill.gstCents);
    document.getElementById('total-2').textContent = money(bill.totalCents);
    document.getElementById('lines').innerHTML = bill.lines.map((line) =>
      '<tr><td>' + line.description + '</td><td>' + line.quantity + '</td><td>' + line.unit + '</td><td class="amount">' + money(line.amountCents) + '</td></tr>'
    ).join('');
    document.getElementById('bands').innerHTML = Object.entries(bill.bandKwh).map(([name, kwh]) =>
      '<tr><td>' + name + '</td><td class="amount">' + kwh + ' kWh</td></tr>'
    ).join('') || '<tr><td colspan="2">No interval usage</td></tr>';
    document.getElementById('status').textContent = 'Period ' + bill.periodStart + ' → ' + bill.periodEnd + ' · ' + bill.activeDays + ' active days.';
  }).catch((error) => { document.getElementById('status').textContent = 'API offline: ' + error.message; });
</script>
</body>
</html>
""";
}
