// oxlint-disable react/only-export-components -- düğüm/kenar tipleri React Flow kayıtlarıyla birlikte yaşar
import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import Typography from '@mui/material/Typography';
import CloseIcon from '@mui/icons-material/Close';
import TableChartOutlinedIcon from '@mui/icons-material/TableChartOutlined';
import {
  BaseEdge, EdgeLabelRenderer, getSmoothStepPath, Handle, NodeResizer, Position,
  type EdgeProps, type Node, type NodeProps,
} from '@xyflow/react';

// ---- Sorgu Tasarımcısı kanvas bileşenleri (Y4 refactor: bölüm 1) ----

export type DugumVeri = {
  ad: string;
  satirlar: { anahtar: string; renk: string; ad: string; ek: string }[];
  bagli: boolean;
  removeField: (anahtar: string) => void;
  removeTable: (tabloId: string) => void;
};
export type TabloNode = Node<DugumVeri, 'tablo'>;

export function TableNode({ id, data, selected }: NodeProps<TabloNode>) {
  return (
    <Box sx={{
      width: '100%', height: '100%', minWidth: 170, minHeight: 60,
      display: 'flex', flexDirection: 'column',
      borderRadius: '8px', overflow: 'hidden', bgcolor: 'background.paper',
      border: data.bagli ? (selected ? '1.5px solid #0e6f7a' : '1px solid #d5dde6') : '1.5px solid #d9534f',
      boxShadow: '0 4px 14px rgba(15,45,78,.12)', fontFamily: '"Segoe UI",Roboto,sans-serif',
    }}>
      {/* seçiliyken kenar/köşelerden boyutlandırma */}
      <NodeResizer isVisible={selected} minWidth={170} minHeight={60}
        lineStyle={{ borderColor: 'secondary.main' }} handleStyle={{ background: 'secondary.main', width: 7, height: 7, borderRadius: 2 }} />
      <Handle type="target" position={Position.Left} style={{ opacity: 0 }} />
      <Handle type="source" position={Position.Right} style={{ opacity: 0 }} />
      <Box sx={{ pl: 1.2, pr: 0.5, py: 0.4, color: '#fff', fontSize: 12, fontWeight: 700, display: 'flex', alignItems: 'center', gap: 0.6, background: 'linear-gradient(135deg,#0f2d4e,#0e6f7a)', flexShrink: 0 }}>
        <TableChartOutlinedIcon sx={{ fontSize: 14 }} />
        <Box component="span" sx={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', flexGrow: 1 }}>{data.ad}</Box>
        <IconButton className="nodrag" size="small" title="Tabloyu sorgudan çıkar"
          onClick={(e) => { e.stopPropagation(); data.removeTable(id); }}
          sx={{ color: 'rgba(255,255,255,.75)', p: 0.25, '&:hover': { color: '#fff', bgcolor: 'rgba(255,255,255,.15)' } }}>
          <CloseIcon sx={{ fontSize: 13 }} />
        </IconButton>
      </Box>
      {/* alan listesi: TAMAMI listelenir; sığmazsa düğüm içinde kaydırılır (nowheel: tekerlek
          kanvası değil listeyi kaydırır). Daha fazla yer için düğümü köşesinden büyüt. */}
      <Box className="nodrag nowheel" sx={{ px: 1.2, py: 0.6, flexGrow: 1, overflowY: 'auto', minHeight: 0, bgcolor: 'background.paper' }}>
        {data.satirlar.length === 0 && <Typography sx={{ fontSize: 10.5, color: 'text.disabled' }}>filtrede kullanılıyor</Typography>}
        {data.satirlar.map((s) => (
          <Box key={s.anahtar} sx={{ display: 'flex', alignItems: 'center', gap: 0.7, py: '1px', '&:hover .removeField': { opacity: 1 } }}>
            <Box sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: s.renk, flexShrink: 0 }} />
            <Typography noWrap sx={{ fontSize: 11, color: 'text.secondary', flexGrow: 1 }}>{s.ad}</Typography>
            {s.ek && <Typography sx={{ fontSize: 10, fontWeight: 700, color: 'success.main' }}>{s.ek}</Typography>}
            <IconButton className="removeField" size="small" title="Alanı kaldır"
              onClick={(e) => { e.stopPropagation(); data.removeField(s.anahtar); }}
              sx={{ p: 0.15, opacity: 0, transition: 'opacity .12s', color: 'text.secondary', '&:hover': { color: 'error.main' } }}>
              <CloseIcon sx={{ fontSize: 11 }} />
            </IconButton>
          </Box>
        ))}
        {!data.bagli && <Typography sx={{ fontSize: 10, fontWeight: 700, color: 'error.main', mt: 0.3 }}>⚠ join yolu yok</Typography>}
      </Box>
    </Box>
  );
}
export const dugumTurleri = { tablo: TableNode };

// Join kenarı: yerleşik `label` prop'u pointer-events kapalı olduğundan tıklanamıyordu.
// Bu özel edge etiketi GERÇEK bir HTML çip olarak çizer (pointerEvents:'all') → HER kenar
// tıklanabilir; tıklanınca data.ac() join-kolon diyaloğunu açar. Eşitliğin her tarafında kolon
// TABLOSUYLA yazılır (Talepler.ServisId = Servisler.Id) — hangi alan hangi tabloya ait, net.
// Kullanıcı kolonları elle değiştirdiyse (ozel) çip vurgulu görünür.
export type JoinEdgeData = { etiket: string; ozel: boolean; ac: () => void };
export function JoinEdge({ sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, markerEnd, data }: EdgeProps) {
  const [yol, etiketX, etiketY] = getSmoothStepPath({ sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition });
  const d = (data ?? {}) as JoinEdgeData;
  return (
    <>
      <BaseEdge path={yol} markerEnd={markerEnd} style={{ stroke: '#0e6f7a', strokeWidth: d.ozel ? 2.4 : 1.8 }} />
      <EdgeLabelRenderer>
        <div
          className="nodrag nopan"
          onClick={(e) => { e.stopPropagation(); d.ac(); }}
          title="Join kolonlarını değiştir — bu iki tablo hangi alanlardan bağlansın?"
          style={{
            position: 'absolute', transform: `translate(-50%,-50%) translate(${etiketX}px,${etiketY}px)`,
            pointerEvents: 'all', cursor: 'pointer',
            display: 'inline-flex', alignItems: 'center', gap: 4,
            padding: '2px 8px', borderRadius: 6, fontSize: 10, whiteSpace: 'nowrap',
            fontFamily: '"Segoe UI",Roboto,sans-serif',
            fontWeight: d.ozel ? 700 : 500, color: 'secondary.main',
            background: d.ozel ? 'var(--kds-tealdolgu)' : 'var(--kds-tealdolgu)',
            border: `1px solid ${d.ozel ? '#6cc0cb' : '#bfe0e5'}`,
            boxShadow: '0 1px 4px rgba(14,111,122,.18)',
          }}>
          {d.etiket}
          <span style={{ fontSize: 11, opacity: 0.85 }}>✎</span>
        </div>
      </EdgeLabelRenderer>
    </>
  );
}
export const kenarTurleri = { join: JoinEdge };
