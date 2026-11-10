import { useEffect, useRef } from 'react';
// Monaco 0.56 (MIT): kök paket editörü + dil kayıtlarını (SQL dahil) getirir; worker ayrı girişten.
import * as monaco from 'monaco-editor';
import EditorWorker from 'monaco-editor/editor/editor.worker.start.js?worker';
import { useTemaModu } from '../temaContext';

(globalThis as { MonacoEnvironment?: monaco.Environment }).MonacoEnvironment = {
  getWorker: () => new EditorWorker(),
};

/** Monaco tabanlı SQL editörü (S9). Ctrl+Enter = çalıştır. S25: tema moduna uyar (koyu → vs-dark). */
export function SqlEditor({ deger, degisti, calistir, yukseklik = 260 }: {
  deger: string;
  degisti: (yeni: string) => void;
  calistir: () => void;
  yukseklik?: number;
}) {
  const kap = useRef<HTMLDivElement>(null);
  const editor = useRef<monaco.editor.IStandaloneCodeEditor | null>(null);
  const calistirRef = useRef(calistir); calistirRef.current = calistir;
  const changedRef = useRef(degisti); changedRef.current = degisti;
  const ilkDeger = useRef(deger);
  const { mod } = useTemaModu();

  useEffect(() => {
    const ed = monaco.editor.create(kap.current!, {
      value: ilkDeger.current,
      language: 'sql',
      theme: document.documentElement.dataset.mod === 'koyu' ? 'vs-dark' : 'vs',
      fontSize: 13,
      fontFamily: 'Consolas, "Cascadia Code", ui-monospace, monospace',
      minimap: { enabled: false },
      automaticLayout: true,
      scrollBeyondLastLine: false,
      wordWrap: 'on',
      tabSize: 2,
      padding: { top: 10, bottom: 10 },
      renderLineHighlight: 'line',
    });
    editor.current = ed;
    ed.onDidChangeModelContent(() => changedRef.current(ed.getValue()));
    ed.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter, () => calistirRef.current());
    return () => ed.dispose();
  }, []);

  // Dışarıdan değer değişirse (ör. örnek sorgu yükleme) editörü eşitle.
  useEffect(() => {
    const ed = editor.current;
    if (ed && ed.getValue() !== deger) ed.setValue(deger);
  }, [deger]);

  // Tema değişince editör de geçsin (Monaco teması global API'dendir).
  useEffect(() => {
    monaco.editor.setTheme(mod === 'koyu' ? 'vs-dark' : 'vs');
  }, [mod]);

  return <div ref={kap} style={{ height: yukseklik, width: '100%' }} />;
}
