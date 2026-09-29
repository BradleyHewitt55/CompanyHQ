export function safeReturnTo(value: string | undefined) {
  if (!value || !value.startsWith('/') || /[\\\r\n]/.test(value) || value.startsWith('//')) return '/';
  const url = new URL(value, 'https://app.invalid');
  return url.origin === 'https://app.invalid' && !/^\/(login|signup|api)(\/|$)/.test(url.pathname)
    ? `${url.pathname}${url.search}` : '/';
}
