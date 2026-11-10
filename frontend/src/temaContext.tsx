// oxlint-disable react/only-export-components -- sağlayıcı ve hook birlikte yaşar
import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';
import { ThemeProvider } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import { temaYap, type TemaModu } from './theme';

/** Açık/koyu tema (21 Ağu 2026 mockup kararı): tek kimlik Lacivert & Teal, iki mod.
 * Tercih localStorage'da kalıcıdır — kullanıcı login'de veya rayda değiştirir, hep öyle kalır. */
const ANAHTAR = 'kds.temaModu';

const TemaContext = createContext<{ mod: TemaModu; degistir: () => void }>({
  mod: 'acik',
  degistir: () => {},
});

export function useTemaModu() {
  return useContext(TemaContext);
}

function kayitliMod(): TemaModu {
  try {
    return localStorage.getItem(ANAHTAR) === 'koyu' ? 'koyu' : 'acik';
  } catch {
    return 'acik';
  }
}

export function TemaSaglayici({ children }: { children: ReactNode }) {
  const [mod, setMod] = useState<TemaModu>(kayitliMod);
  // index.css'teki --kds-* yumuşak dolgu token'ları bu işarete bakar (sx var(--kds-…) kullanımı).
  document.documentElement.dataset.mod = mod;
  const deger = useMemo(
    () => ({
      mod,
      degistir: () => {
        setMod((m) => {
          const yeni: TemaModu = m === 'acik' ? 'koyu' : 'acik';
          try { localStorage.setItem(ANAHTAR, yeni); } catch { /* gizli sekme vb. — tercih uçar, sorun değil */ }
          return yeni;
        });
      },
    }),
    [mod],
  );
  const tema = useMemo(() => temaYap(mod), [mod]);
  return (
    <TemaContext.Provider value={deger}>
      <ThemeProvider theme={tema}>
        <CssBaseline />
        {children}
      </ThemeProvider>
    </TemaContext.Provider>
  );
}
