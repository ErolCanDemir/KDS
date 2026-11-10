import { useEffect, useRef } from 'react';
import * as echarts from 'echarts/core';
import { BarChart, LineChart, PieChart } from 'echarts/charts';
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import type { EChartsCoreOption } from 'echarts/core';

// Tree-shaken ECharts (Apache-2.0): yalnız kullandığımız grafikler + bileşenler paketlenir.
echarts.use([BarChart, LineChart, PieChart, GridComponent, TooltipComponent, LegendComponent, CanvasRenderer]);

/** İnce ECharts sarmalayıcı: init + setOption + pencereyle resize + dispose. */
export function EChart({ secenek, yukseklik = 380 }: { secenek: EChartsCoreOption; yukseklik?: number }) {
  const kap = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const grafik = echarts.init(kap.current!);
    grafik.setOption(secenek);
    const boyutla = () => grafik.resize();
    window.addEventListener('resize', boyutla);
    return () => { window.removeEventListener('resize', boyutla); grafik.dispose(); };
  }, [secenek]);

  return <div ref={kap} style={{ width: '100%', height: yukseklik }} />;
}
