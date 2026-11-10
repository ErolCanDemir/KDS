import { useState, type ReactNode } from 'react';
import Button from '@mui/material/Button';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import ListItemIcon from '@mui/material/ListItemIcon';
import CircularProgress from '@mui/material/CircularProgress';
import DownloadIcon from '@mui/icons-material/Download';
import GridOnIcon from '@mui/icons-material/GridOn';
import DescriptionOutlinedIcon from '@mui/icons-material/DescriptionOutlined';
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdf';
import type { SxProps, Theme } from '@mui/material/styles';
import type { ExportFormat } from '../api/export';

const secenekler: { bicim: ExportFormat; ad: string; ikon: ReactNode }[] = [
  { bicim: 'Xlsx', ad: 'Excel (.xlsx)', ikon: <GridOnIcon fontSize="small" /> },
  { bicim: 'Csv', ad: 'CSV (.csv)', ikon: <DescriptionOutlinedIcon fontSize="small" /> },
  { bicim: 'Pdf', ad: 'PDF (.pdf)', ikon: <PictureAsPdfIcon fontSize="small" /> },
];

/** "Dışa Aktar" butonu + Excel/CSV/PDF menüsü. İndirme sürerken göstergeli. Hata gösterimi
 * çağırana ait (onDisaAktar throw ederse burada yutulmaz — sayfa Alert'i yakalar). */
export function ExportMenu({ onDisaAktar, disabled, variant = 'outlined', sx }: {
  onDisaAktar: (bicim: ExportFormat) => Promise<void>;
  disabled?: boolean;
  variant?: 'text' | 'outlined' | 'contained';
  sx?: SxProps<Theme>;
}) {
  const [anchor, setAnchor] = useState<null | HTMLElement>(null);
  const [mesgul, setMesgul] = useState(false);

  async function sec(bicim: ExportFormat) {
    setAnchor(null);
    setMesgul(true);
    try {
      await onDisaAktar(bicim);
    } finally {
      setMesgul(false);
    }
  }

  return (
    <>
      <Button variant={variant} sx={sx} disabled={disabled || mesgul}
        startIcon={mesgul ? <CircularProgress size={14} color="inherit" /> : <DownloadIcon />}
        onClick={(e) => setAnchor(e.currentTarget)}>
        Dışa Aktar
      </Button>
      <Menu anchorEl={anchor} open={anchor !== null} onClose={() => setAnchor(null)}>
        {secenekler.map((s) => (
          <MenuItem key={s.bicim} onClick={() => void sec(s.bicim)}>
            <ListItemIcon>{s.ikon}</ListItemIcon>{s.ad}
          </MenuItem>
        ))}
      </Menu>
    </>
  );
}
