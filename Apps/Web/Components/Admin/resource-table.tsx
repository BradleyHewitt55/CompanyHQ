import Link from 'next/link';
import type { ReactNode } from 'react';
import type { AdminResource, AdminRecord } from '../admin-data';
export function ResourceTable({ resource, records, actions }: { resource: AdminResource; records: readonly AdminRecord[]; actions?: (record: AdminRecord) => ReactNode }) {
return (          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  {resource.columns.map((column) => <th key={column} scope="col">{column}</th>)}
                  {actions ? <th scope="col"><span className="visually-hidden">Actions</span></th> : null}
                </tr>
              </thead>
              <tbody data-infinite-scroll-list="true">
                {records.map((item) => (
                  <tr key={item.id}>
                    <td>
                      <Link className="record-link" href={`/admin/${resource.key}/${item.id}`}>
                        <span className="record-title">{item.title}</span>
                        <span className="record-subtitle">{item.subtitle}</span>
                      </Link>
                    </td>
                    <td>{resource.key === 'inbox' && item.source ? <><span className="status-pill neutral">{item.source}</span><span className="record-subtitle block">{item.meta}</span></> : item.meta}</td>
                    <td><StatusPill status={item.status} tone={item.statusTone} /></td>
                    <td className="muted-cell">{item.updated}</td>
                    {actions ? <td className="row-actions">{actions(item)}</td> : null}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>);
}
function StatusPill({ status, tone }: { status: string; tone: string }) {
  return <span className={`status-pill ${tone}`}>{status}</span>;
}
