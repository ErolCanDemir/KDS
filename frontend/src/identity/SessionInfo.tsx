import { useEffect, useState, type ReactNode } from 'react';
import { useAuth } from 'react-oidc-context';
import { apiGet } from '../api/client';
import { bosOturum, OturumBaglami, type SessionInfo } from './sessionContext';

type KimlikYaniti = { ad?: string; roller?: string[]; tenantId?: string };

/** Kimlik kapalıyken (Keycloak yok) boş oturum sağlar — useAuth çağırmaz. */
export function BosOturumSaglayici({ children }: { children: ReactNode }) {
  return <OturumBaglami.Provider value={bosOturum}>{children}</OturumBaglami.Provider>;
}

/** AuthProvider içinde: backend'in doğruladığı kimliği (ad + roller + tenant) çeker
 * ve tüm ekranlara sağlar. Backend whoami tek doğruluk kaynağıdır. */
export function OturumSaglayici({ children }: { children: ReactNode }) {
  const kimlik = useAuth();
  const [oturum, setOturum] = useState<SessionInfo>({ roller: [], yukleniyor: true });

  useEffect(() => {
    if (!kimlik.isAuthenticated || !kimlik.user) return;
    let iptalEdildi = false;
    apiGet<KimlikYaniti>('/api/durum/kimlik', kimlik.user.access_token)
      .then((y) => {
        if (iptalEdildi) return;
        setOturum({ ad: y.ad, roller: y.roller ?? [], tenantId: y.tenantId, yukleniyor: false });
      })
      .catch((h: Error) => {
        if (!iptalEdildi) setOturum({ roller: [], yukleniyor: false, hata: h.message });
      });
    return () => {
      iptalEdildi = true;
    };
  }, [kimlik.isAuthenticated, kimlik.user]);

  return <OturumBaglami.Provider value={oturum}>{children}</OturumBaglami.Provider>;
}
