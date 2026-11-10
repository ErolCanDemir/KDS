import { useEffect, type ReactNode } from 'react';
import Alert from '@mui/material/Alert';
import { useAuth } from 'react-oidc-context';
import { kimlikKapali } from './identityConfig';
import { LoadingCurtain } from '../components/Loading';

/** Altındaki her şey oturum ister: oturum yoksa Keycloak'a yönlendirir.
 * Kimlik bayrakla kapalıyken (S3 öncesi) koruma uygulanmaz. */
export function ProtectedArea({ children }: { children: ReactNode }) {
  if (kimlikKapali) return children;
  return <KimlikBekcisi>{children}</KimlikBekcisi>;
}

function KimlikBekcisi({ children }: { children: ReactNode }) {
  const kimlik = useAuth();

  useEffect(() => {
    if (!kimlik.isLoading && !kimlik.isAuthenticated && !kimlik.activeNavigator && !kimlik.error) {
      void kimlik.signinRedirect();
    }
  }, [kimlik]);

  if (kimlik.error) {
    return <Alert severity="error">Kimlik doğrulama hatası: {kimlik.error.message}</Alert>;
  }
  if (!kimlik.isAuthenticated) {
    return <LoadingCurtain mesaj="Oturum doğrulanıyor…" />;
  }
  return children;
}
