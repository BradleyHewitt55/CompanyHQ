import Link from 'next/link';
import type { AdminResource, AdminRecord } from '../admin-data';
export function ResourceTable({ resource, records }: { resource: AdminResource; records: readonly AdminRecord[] }) {
return (          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  {resource.columns.map((column) => <th key={column} scope="col">{column}</th>)}
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
                    <td>{item.meta}</td>
                    <td><StatusPill status={item.status} tone={item.statusTone} /></td>
                    <td className="muted-cell">{item.updated}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>);
}
function StatusPill({ status, tone }: { status: string; tone: string }) {
  return <span className={`status-pill ${tone}`}>{status}</span>;
}
