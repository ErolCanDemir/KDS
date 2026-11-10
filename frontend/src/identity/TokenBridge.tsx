import { useEffect } from 'react';
import { useAuth } from 'react-oidc-context';
import { setToken, setSessionRefresher } from './tokenStore';

/** Oturum jetonunu ve "yeniden giriş" eylemini modül deposuna aktarır (yenileme dahil);
 * AuthProvider içinde bir kez render edilir. Görsel çıktısı yoktur. */
export function TokenBridge() {
  const kimlik = useAuth();

  useEffect(() => {
    setToken(kimlik.user?.access_token);
  }, [kimlik.user]);

  useEffect(() => {
    setSessionRefresher(() => void kimlik.signinRedirect());
    return () => setSessionRefresher(undefined);
  }, [kimlik]);

  return null;
}
