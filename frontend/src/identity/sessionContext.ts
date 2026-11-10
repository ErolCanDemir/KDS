import { createContext, useContext } from 'react';

export type SessionInfo = {
  ad?: string;
  roller: string[];
  tenantId?: string;
  yukleniyor: boolean;
  hata?: string;
};

export const bosOturum: SessionInfo = { roller: [], yukleniyor: false };

export const OturumBaglami = createContext<SessionInfo>(bosOturum);

export function useSession(): SessionInfo {
  return useContext(OturumBaglami);
}
