'use client';

import Link from 'next/link';
import { quickLinks, type DashboardData } from '@/Components/admin-data';
import { Icon } from '@/Components/icons';

export function DashboardContent({ data, permissions }: { data: DashboardData; permissions: readonly string[] }) {
  return (
    <section aria-labelledby="dashboard-title" className="dashboard-page">
      <h1 className="visually-hidden" id="dashboard-title">Overview</h1>

      <div className="stats-grid">
        {data.stats.map((stat) => {
          const live = stat.change === 'Live from API';
          return (
            <Link className="stat-card" href={stat.href} key={stat.label}>
              <div className="stat-card-header">
                <span className="stat-card-label">{stat.label}</span>
                <span className={`stat-card-icon ${stat.tone}`}><Icon name={stat.icon} size={20} /></span>
              </div>
              <strong className="stat-card-value">{stat.value}</strong>
              <span className={`stat-card-change ${live ? 'live' : stat.change.startsWith('-') ? 'negative' : 'positive'}`}>
                <Icon name={live ? 'activity' : stat.change.startsWith('-') ? 'arrow-down' : 'arrow-up'} size={14} />
                {stat.change}
              </span>
            </Link>
          );
        })}
      </div>

      <div className="content-grid">
        <section aria-labelledby="activity-title" className="dashboard-panel">
          <div className="card-header">
            <h2 className="card-title" id="activity-title">Recent activity</h2>
          </div>
          <div className="card-body">
            {data.recentActivity.length > 0 ? (
              <div className="activity-list">
                {data.recentActivity.map((item) => (
                  <div className="activity-item" key={`${item.initials}-${item.time}-${item.text}`}>
                    <span className={`activity-avatar ${item.tone}`}>{item.initials}</span>
                    <span className="activity-copy">
                      <strong>{item.text}</strong>
                      <small>{item.time}</small>
                    </span>
                  </div>
                ))}
              </div>
            ) : (
              <div className="empty-state compact-empty-state">
                <h2 className="section-title">No recent activity</h2>
                <p>The API has not returned any recent records.</p>
              </div>
            )}
          </div>
        </section>

        <section aria-labelledby="quick-links-title" className="dashboard-panel">
          <div className="card-header">
            <h2 className="card-title" id="quick-links-title">Quick links</h2>
          </div>
          <div className="card-body">
            <div className="quick-links">
              {quickLinks.filter((link) => permissions.includes(link.permission)).map((link) => (
                <Link className="quick-link" href={link.href} key={link.href}>
                  <span className="quick-link-icon"><Icon name={link.icon} size={18} /></span>
                  <span>{link.label}</span>
                  <Icon className="quick-link-arrow" name="chevron-down" size={15} />
                </Link>
              ))}
            </div>
          </div>
        </section>
      </div>
    </section>
  );
}

