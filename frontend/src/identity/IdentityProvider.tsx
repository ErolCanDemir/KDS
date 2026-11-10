import type { ReactNode } from 'react';
import { AuthProvider } from 'react-oidc-context';
import { TokenBridge } from './TokenBridge';
import { kimlikKapali, oidcAyar } from './identityConfig';
import { BosOturumSaglayici, OturumSaglayici } from './SessionInfo';

/** Kimlik açıkken uygulamayı OIDC sağlayıcısına sarar, jeton köprüsünü ve oturum
 * bilgisini kurar; kapalıyken (Keycloak yoksa) boş oturumla saydamdır. */
export function IdentityProvider({ children }: { children: ReactNode }) {
  if (kimlikKapali) return <BosOturumSaglayici>{children}</BosOturumSaglayici>;
  return (
    <AuthProvider {...oidcAyar}>
      <TokenBridge />
      <OturumSaglayici>{children}</OturumSaglayici>
    </AuthProvider>
  );
}
