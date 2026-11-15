export function writeContractTerms(articles) {
  return articles.map(({ title, body }, index) => {
    // Indent each body line so pasted article headings stay inside the article.
    const lines = body.trim().split(/\r?\n/).map(line => '  ' + line);
    return [`Điều ${index + 1}: ${title.trim()}`, ...lines].join('\n');
  }).join('\n\n');
}

export function readContractTerms(terms, paymentTerms) {
  const text = (terms ?? '').replace(/\r\n/g, '\n').trim();
  const parts = text ? text.split(/\n\n(?=Điều [0-9]+: )/) : [];
  const parsed = parts.map(part => /^Điều ([0-9]+): ([^\n]+)\n([\s\S]*)$/.exec(part));
  const articles = parsed.every(Boolean)
    ? parsed.map(([, number, title, body]) => ({
      number: Number(number), title, body: body.replace(/^  /gm, '').trim(),
    }))
    : [{ number: 1, title: 'Nội dung hợp đồng', body: text }];
  if (paymentTerms?.trim()) {
    const oldTemplate = articles.map(({ number }) => number).join(',') === '1,2,3,4,6,7,8';
    const payment = { title: 'Thanh toán', body: paymentTerms.trim() };
    if (oldTemplate) articles.splice(4, 0, payment);
    else articles.push(payment);
  }
  return articles.map(({ title, body }) => ({ title, body }));
}
