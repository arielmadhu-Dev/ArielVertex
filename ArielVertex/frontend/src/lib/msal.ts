import { api, tokenStore } from './api'
import type { CurrentUser } from './types'

const clientId = import.meta.env.VITE_MSAL_CLIENT_ID?.trim()
const tenantId = import.meta.env.VITE_MSAL_TENANT_ID?.trim()

/** Microsoft login is only offered when the SPA is configured with an Entra app registration. */
export const microsoftEnabled = !!(clientId && tenantId)

/**
 * Signs in with Microsoft via MSAL popup, then exchanges the Entra id-token for the app's own JWT
 * at /auth/microsoft. MSAL is imported lazily so it's only pulled in when actually used.
 */
export async function signInWithMicrosoft(): Promise<CurrentUser> {
  if (!microsoftEnabled) throw new Error('Microsoft login is not configured.')
  const { PublicClientApplication } = await import('@azure/msal-browser')
  const pca = new PublicClientApplication({
    auth: {
      clientId: clientId!,
      authority: `https://login.microsoftonline.com/${tenantId}`,
      redirectUri: window.location.origin,
    },
    cache: { cacheLocation: 'sessionStorage' },
  })
  await pca.initialize()
  const result = await pca.loginPopup({ scopes: ['openid', 'profile', 'email', 'User.Read'] })
  const token = result.idToken
  const { data } = await api.post<{ token: string; user: CurrentUser }>('/auth/microsoft', { token })
  tokenStore.set(data.token)
  return data.user
}
