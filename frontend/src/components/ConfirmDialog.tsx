import { useCallback, useRef, useState, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogContent from '@mui/material/DialogContent';
import Typography from '@mui/material/Typography';
import WarningAmberRoundedIcon from '@mui/icons-material/WarningAmberRounded';
import HelpOutlineRoundedIcon from '@mui/icons-material/HelpOutlineRounded';
import { boxRadius } from '../theme';

type ConfirmOptions = {
  mesaj: string;
  baslik?: string;
  onayYazisi?: string;   // varsayılan "Evet"
  tehlikeli?: boolean;   // silme gibi geri alınamaz işlem → kırmızı onay butonu
};

/** Tarayıcının çirkin `window.confirm`'i yerine ürün diliyle onay diyaloğu.
 * Kullanım: `const { askConfirm, confirmDialog } = useConfirm();` → JSX'e `{confirmDialog}` koy,
 * işlemde `if (!(await askConfirm('… silinsin mi?', { tehlikeli: true }))) return;` */
export function useConfirm(): { askConfirm: (mesaj: string, ek?: Omit<ConfirmOptions, 'mesaj'>) => Promise<boolean>; confirmDialog: ReactNode } {
  const [ayar, setAyar] = useState<ConfirmOptions | null>(null);
  const cozRef = useRef<((v: boolean) => void) | null>(null);

  const askConfirm = useCallback((mesaj: string, ek?: Omit<ConfirmOptions, 'mesaj'>) =>
    new Promise<boolean>((coz) => { cozRef.current = coz; setAyar({ mesaj, ...ek }); }), []);
  const close = (v: boolean) => { cozRef.current?.(v); cozRef.current = null; setAyar(null); };

  const confirmDialog = (
    <Dialog open={ayar !== null} onClose={() => close(false)} maxWidth="xs" fullWidth
      slotProps={{ paper: { sx: { borderRadius: boxRadius } } }}>
      {ayar && (
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.5, pt: 2.5 }}>
          <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1.5 }}>
            <Box sx={{
              width: 40, height: 40, borderRadius: '50%', flexShrink: 0,
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              bgcolor: ayar.tehlikeli ? 'rgba(217,83,79,.12)' : 'rgba(14,111,122,.12)',
              color: ayar.tehlikeli ? 'error.main' : 'secondary.main',
            }}>
              {ayar.tehlikeli ? <WarningAmberRoundedIcon /> : <HelpOutlineRoundedIcon />}
            </Box>
            <Box>
              <Typography sx={{ fontWeight: 700, fontSize: 15.5, mb: 0.5 }} color="primary">
                {ayar.baslik ?? (ayar.tehlikeli ? 'Emin misin?' : 'Onay')}
              </Typography>
              <Typography sx={{ fontSize: 13.5 }} color="text.secondary">{ayar.mesaj}</Typography>
            </Box>
          </Box>
          <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1 }}>
            <Button color="inherit" onClick={() => close(false)}>Vazgeç</Button>
            <Button variant="contained" color={ayar.tehlikeli ? 'error' : 'secondary'} autoFocus
              onClick={() => close(true)}>
              {ayar.onayYazisi ?? 'Evet'}
            </Button>
          </Box>
        </DialogContent>
      )}
    </Dialog>
  );

  return { askConfirm, confirmDialog };
}
