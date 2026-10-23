/**
 * DareCare WEB-only integration starter. In-memory credentials only.
 * NOT production auth storage; for a hardened web app use a BFF + HttpOnly cookies.
 * Never put the refresh token in localStorage or print it to logs.
 */
export function createDareCareClient(baseUrl, onSessionExpired = () => {}) {
  const root = baseUrl.replace(/\/$/, '');
  if (!/^https:\/\//i.test(root) && !/^http:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/i.test(root)) {
    throw new Error('DareCare API requires HTTPS unless you are on localhost.');
  }
  let accessToken = null;
  let refreshToken = null;

  function setSession(result) {
    if (!result?.accessToken || !result?.refreshToken) throw Error('Unexpected AuthResponse');
    accessToken = result.accessToken;
    refreshToken = result.refreshToken;
  }
  function clearSession() { accessToken = null; refreshToken = null; }
  async function parse(response) {
    const type = response.headers.get('content-type') || '';
    const data = type.includes('application/json') || type.includes('+json') ? await response.json() : null;
    if (!response.ok) {
      let error = 'Request failed';
      if (data?.errors) error = Object.values(data.errors).flat().join(' · ') || error;
      else error = data?.detail || data?.message || data?.title || error;
      const e = new Error(`${response.status}: ${error}`);
      e.status = response.status;
      throw e;
    }
    return data;
  }
  async function refresh() {
    if (!refreshToken) return false;
    const response = await fetch(`${root}/api/v1/auth/refresh`, {
      method: 'POST', headers: {'Content-Type':'application/json'},
      body: JSON.stringify({refreshToken}), cache: 'no-store'
    });
    if (!response.ok) return false;
    setSession(await response.json());
    return true;
  }
  async function request(path, {method='GET', body, ...rest} = {}) {
    const run = () => fetch(`${root}${path}`, {
      ...rest, method, cache: 'no-store',
      headers: {
        ...(body instanceof FormData ? {} : body !== undefined ? {'Content-Type':'application/json'} : {}),
        ...(accessToken ? {Authorization:`Bearer ${accessToken}`} : {}),
        ...(rest.headers || {}),
      },
      ...(body !== undefined ? {body:body instanceof FormData ? body : JSON.stringify(body)} : {}),
    });
    let response = await run();
    if (response.status === 401 && accessToken && await refresh()) response = await run(); // once only
    else if (response.status === 401 && accessToken) { clearSession(); onSessionExpired(); }
    return parse(response);
  }
  async function login(email, password) {
    const result = await request('/api/v1/auth/login', {method:'POST', body:{email,password}});
    setSession(result);
    return {userId:result.userId, roles:result.roles, expiresAtUtc:result.expiresAtUtc};
  }
  async function logout() {
    try { if (accessToken && refreshToken) await request('/api/v1/auth/logout', {method:'POST', body:{refreshToken}}); }
    finally { clearSession(); }
  }
  return {request, login, logout, clearSession, isSignedIn:()=>!!accessToken};
}
