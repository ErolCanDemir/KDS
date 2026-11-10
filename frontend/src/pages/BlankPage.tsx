import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import ConstructionIcon from '@mui/icons-material/Construction';
import { boxRadius } from '../theme';
import { PageHeader } from '../components/PageHeader';

/** Henüz dilimi gelmemiş ekranların yer tutucusu — theme başlık şeridi +
 * hangi dilimde geleceğini söyleyen kart. */
export function BlankPage({ baslik, aciklama }: { baslik: string; aciklama: string }) {
  return (
    <Box>
      <PageHeader ikon={<ConstructionIcon />} baslik={baslik} aciklama="Bu ekran yol haritasında — henüz gelmedi." />
      <Paper
        variant="outlined"
        sx={{ p: 4, borderRadius: boxRadius, maxWidth: 640, display: 'flex', alignItems: 'center', gap: 2, borderStyle: 'dashed' }}
      >
        <ConstructionIcon sx={{ color: 'secondary.main', fontSize: 34 }} />
        <Typography variant="body2" color="text.secondary">{aciklama}</Typography>
      </Paper>
    </Box>
  );
}
