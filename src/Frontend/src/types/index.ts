export type BottleCounts = { glass: number; metal: number; plastic: number };

export type Visitor = {
  id: number;
  total: number;
  remaining: number;
  bottles: BottleCounts;
  waiting?: boolean;
};

export type Recycler = {
  id: number;
  level: number;
  capacity: number;
  currentBottles: BottleCounts;
  visitor?: Visitor | null;
};

export type TruckStatus = 'idle' | 'to_recycler' | 'loading' | 'to_plant' | 'delivering' | 'picking';

export type Truck = {
  id: number;
  level: number;
  capacity: number;
  currentLoad: number;
  status: TruckStatus;
  targetRecyclerId?: number | null;
  cargo?: BottleCounts | null;
};

export type LogEntry = {
  id: string;
  time: string;
  type: 'info' | 'success' | 'warning' | 'error';
  message: string;
};

export type ChartPoint = {
  timestamp: number;
  glass: number;
  metal: number;
  plastic: number;
};