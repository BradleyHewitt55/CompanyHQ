'use client';

import { createContext, useContext, useEffect, useState, type Dispatch, type ReactNode, type SetStateAction } from 'react';

type AdminBreadcrumbContextValue = {
  detail: string | null;
  setDetail: Dispatch<SetStateAction<string | null>>;
};

const AdminBreadcrumbContext = createContext<AdminBreadcrumbContextValue>({
  detail: null,
  setDetail: () => undefined,
});

export function AdminBreadcrumbProvider({ children }: { children: ReactNode }) {
  const [detail, setDetail] = useState<string | null>(null);
  return <AdminBreadcrumbContext.Provider value={{ detail, setDetail }}>{children}</AdminBreadcrumbContext.Provider>;
}

export function useAdminBreadcrumb() {
  return useContext(AdminBreadcrumbContext);
}

export function useAdminBreadcrumbDetail(label: string) {
  const { setDetail } = useAdminBreadcrumb();

  useEffect(() => {
    setDetail(label);
    return () => setDetail(null);
  }, [label, setDetail]);
}
