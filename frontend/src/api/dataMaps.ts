import { apiGet, apiSend } from './client';
import type { DataMode } from './connections';

export type ColumnRole = 'Boyut' | 'Olcu' | 'Anahtar' | 'Gizli';

export type DataMapSummary = {
  id: string; ad: string; baglantiId: string; baglantiAd: string | null;
  mod: DataMode; tabloSayisi: number; iliskiSayisi: number;
};
export type DataMapColumnDto = { hamAd: string; dostcaAd: string | null; rol: ColumnRole; veriTipi: string | null };
export type DataMapTableDto = { id: string; sema: string; hamAd: string; dostcaAd: string; kolonlar: DataMapColumnDto[] };
export type DataMapRelationshipDto = { kaynakTabloId: string; kaynakKolon: string; hedefTabloId: string; hedefKolon: string; fktenMi: boolean };
export type DataMapDetail = { id: string; ad: string; baglantiId: string; mod: DataMode; tablolar: DataMapTableDto[]; iliskiler: DataMapRelationshipDto[] };
export type SaveDataMapInput = { id?: string | null; ad: string; baglantiId: string; mod: DataMode; tablolar: DataMapTableDto[]; iliskiler: DataMapRelationshipDto[] };

export type SchemaTableDto = { sema: string; ad: string; kolonSayisi: number };
export type SchemaColumnDto = { ad: string; veriTipi: string; nullOlabilir: boolean; birincilAnahtarMi: boolean };
export type SchemaRelationshipDto = { kaynakSema: string; kaynakTablo: string; kaynakKolon: string; hedefSema: string; hedefTablo: string; hedefKolon: string };

export const getDataMaps = () => apiGet<DataMapSummary[]>('/api/haritalar');
export const getDataMap = (id: string) => apiGet<DataMapDetail>(`/api/haritalar/${id}`);
export const addDataMap = (h: SaveDataMapInput) => apiSend<DataMapSummary>('/api/haritalar', 'POST', h);
export const updateDataMap = (id: string, h: SaveDataMapInput) => apiSend<DataMapSummary>(`/api/haritalar/${id}`, 'PUT', { ...h, id });
export const deleteDataMap = (id: string) => apiSend<void>(`/api/haritalar/${id}`, 'DELETE');

const q = (v: string) => encodeURIComponent(v);
export const searchSchemaTables = (baglantiId: string, arama: string) =>
  apiGet<SchemaTableDto[]>(`/api/haritalar/sema/tablolar?baglantiId=${baglantiId}&arama=${q(arama)}`);
export const getSchemaColumns = (baglantiId: string, sema: string, tablo: string) =>
  apiGet<SchemaColumnDto[]>(`/api/haritalar/sema/kolonlar?baglantiId=${baglantiId}&sema=${q(sema)}&tablo=${q(tablo)}`);
export const getSchemaRelationships = (baglantiId: string, sema: string, tablo: string) =>
  apiGet<SchemaRelationshipDto[]>(`/api/haritalar/sema/iliskiler?baglantiId=${baglantiId}&sema=${q(sema)}&tablo=${q(tablo)}`);
