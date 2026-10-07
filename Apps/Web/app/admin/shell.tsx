'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useEffect, useRef, useState, type ReactNode } from 'react';

import { logoutAction } from '../auth/login/actions';
import type { AuthUserDto, ClientCompanyProfileDto } from '../../src/api/types';
import type { PagedResultOfAdminConversationDto } from '../../src/api/types';
import { getResource, resourceFor } from '../../Components/admin-data';
import { CompanyLogo } from '../../Components/company-logo';
import { Icon, type IconName } from '../../Components/icons';
import { Button } from '../../Components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '../../Components/ui/dialog';
import { AdminBreadcrumbProvider, useAdminBreadcrumb } from './breadcrumb';
import { fullName } from '@/lib/utils';
import { areaPermissions } from '../../src/api/access';
import { TopbarSlots } from './topbar-slots';

type AdminShellProps = {
  children: ReactNode;
  company: ClientCompanyProfileDto | null;
  currentUser: AuthUserDto;
  latestClosedConversation: PagedResultOfAdminConversationDto['items'][number] | null;
};

// `visible` decides from the role's permissions whether the entry is offered; absent means always.
type NavItem = {
  label: string;
  icon: IconName;
  href: string;
  visible?: (permissions: readonly string[]) => boolean;
};

const resourceNav = (key: string): NavItem => {
  const resource = getResource(key)!;
  return {
    label: resource.label,
    href: `/admin/${resource.key}`,
    icon: resource.icon,
    visible: (permissions) => resourceFor(key, permissions) !== undefined,
  };
};

const holds = (permission: string) => (permissions: readonly string[]) => permissions.includes(permission);

const mainNav: NavItem[] = [
  { label: 'Dashboard', href: '/admin', icon: 'dashboard' },
  resourceNav('inbox'),
  resourceNav('users'),
];

const serviceNav: NavItem[] = [
  resourceNav('blog'),
  resourceNav('faqs'),
  { label: 'Email Templates', href: '/admin/email-templates', icon: 'envelope', visible: holds(areaPermissions.emailTemplates) },
];

const settingsNav: (NavItem & { description: string })[] = [
  { label: 'Company Profile', description: 'Basic company info', href: '/admin/settings/company', icon: 'settings', visible: holds(areaPermissions.company) },
  { label: 'Roles', description: 'What each role may do', href: '/admin/settings/roles', icon: 'users', visible: holds(areaPermissions.roles) },
  { label: 'User Profile', description: 'Account and preferences', href: '/admin/settings/user', icon: 'users' },
];

const offered = <T extends NavItem>(items: T[], permissions: readonly string[]) =>
  items.filter((item) => item.visible?.(permissions) ?? true);

export default function AdminShell({ children, company, currentUser, latestClosedConversation }: AdminShellProps) {
  return (
    <AdminBreadcrumbProvider>
      <AdminShellFrame company={company} currentUser={currentUser} latestClosedConversation={latestClosedConversation}>
        {children}
      </AdminShellFrame>
    </AdminBreadcrumbProvider>
  );
}

function AdminShellFrame({ children, company, currentUser, latestClosedConversation }: AdminShellProps) {
  const pathname = usePathname() ?? '/admin';
  const { permissions } = currentUser;
  const services = offered(serviceNav, permissions);
  const { detail } = useAdminBreadcrumb();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [action, setAction] = useState<HTMLElement | null>(null);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [settingsPosition, setSettingsPosition] = useState<{ left: number; bottom: number } | null>(null);
  const [userOpen, setUserOpen] = useState(false);
  const settingsTriggerRef = useRef<HTMLDivElement>(null);
  const settingsButtonRef = useRef<HTMLButtonElement>(null);
  const settingsPopoverRef = useRef<HTMLDivElement>(null);

  const settingsActive = pathname.startsWith('/admin/settings');

  useEffect(() => {
    setSidebarOpen(false);
    setUserOpen(false);
    setSettingsOpen(false);
  }, [pathname]);

  useEffect(() => {
    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (!settingsTriggerRef.current?.contains(target) && !settingsPopoverRef.current?.contains(target)) {
        setSettingsOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setUserOpen(false);
        setSettingsOpen(false);
      }
    };

    document.addEventListener('pointerdown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, []);

  useEffect(() => {
    if (!settingsOpen) {
      setSettingsPosition(null);
      return;
    }

    const positionSettings = () => {
      const button = settingsButtonRef.current;
      if (!button) return;

      const rect = button.getBoundingClientRect();
      const popoverWidth = 292;
      const gutter = 8;
      setSettingsPosition({
        left: Math.max(gutter, Math.min(rect.left, window.innerWidth - popoverWidth - gutter)),
        bottom: Math.max(gutter, window.innerHeight - rect.top + gutter),
      });
    };

    positionSettings();
    window.addEventListener('resize', positionSettings);
    window.addEventListener('scroll', positionSettings, true);
    return () => {
      window.removeEventListener('resize', positionSettings);
      window.removeEventListener('scroll', positionSettings, true);
    };
  }, [settingsOpen]);

  const closeSidebar = () => setSidebarOpen(false);

  return (
    <div className="admin-shell">
      <div
        aria-hidden={!sidebarOpen}
        className={`sidebar-overlay${sidebarOpen ? ' is-visible' : ''}`}
        onClick={closeSidebar}
      />

      <aside aria-label="Admin navigation" className={`admin-sidebar${sidebarOpen ? ' is-open' : ''}`} id="admin-navigation">
        <div className="sidebar-brand">
          <Link aria-label={`${company?.name || 'Admin'} dashboard`} className="brand-link" href="/admin" onClick={closeSidebar}>
            <CompanyLogo alt="" className="brand-art brand-art--image" src={company?.logoUrl ?? '/logo-frame3.svg'} />
            <span className="brand-wordmark">
              <strong>{company?.name}</strong>
              <small>Web / Admin</small>
            </span>
          </Link>
        </div>

        <nav className="sidebar-nav" aria-label="Main navigation">
          {offered(mainNav, permissions).map((item) => (
            <SidebarItem item={item} key={item.label} isActive={isActivePath(pathname, item.href)} onNavigate={closeSidebar} />
          ))}

          {services.length > 0 ? (
            <>
              <div className="sidebar-divider" />
              <div className="sidebar-section-heading">
                <span>Services</span>
                <Icon name="plus" size={14} />
              </div>

              <div className="service-nav">
                {services.map((item) => (
                  <SidebarItem item={item} key={item.label} isActive={isActivePath(pathname, item.href)} onNavigate={closeSidebar} />
                ))}
              </div>
            </>
          ) : null}
        </nav>

        <div className="sidebar-footer">
          <div className="settings-nav" ref={settingsTriggerRef}>
            <button
              ref={settingsButtonRef}
              aria-expanded={settingsOpen}
              className={`nav-link nav-link-button${settingsActive ? ' is-active' : ''}`}
              onClick={() => setSettingsOpen((open) => !open)}
              type="button"
            >
              <span className="nav-icon"><Icon name="settings" /></span>
              <span className="nav-label">Settings</span>
              <Icon className={`settings-chevron${settingsOpen || settingsActive ? ' is-open' : ''}`} name="chevron-down" size={14} />
            </button>
          </div>
        </div>
      </aside>

      {settingsOpen ? (
        <div
          aria-label="Settings options"
          className="settings-popover"
          ref={settingsPopoverRef}
          role="dialog"
          style={settingsPosition ? { left: `${settingsPosition.left}px`, bottom: `${settingsPosition.bottom}px` } : undefined}
        >
          {offered(settingsNav, permissions).map((item) => (
            <Link
              className={`settings-popover-item${pathname === item.href ? ' is-active' : ''}`}
              href={item.href}
              key={item.href}
              onClick={() => {
                closeSidebar();
                setSettingsOpen(false);
              }}
            >
              <span className="nav-icon"><Icon name={item.icon} size={18} /></span>
              <span className="settings-popover-info">
                <strong>{item.label}</strong>
                <small>{item.description}</small>
              </span>
            </Link>
          ))}
        </div>
      ) : null}

      <main className="admin-main">
        <header className="admin-topbar">
          <div className="topbar-left">
            <button
              aria-controls="admin-navigation"
              aria-expanded={sidebarOpen}
              aria-label="Open navigation"
              className="mobile-menu-button"
              onClick={() => setSidebarOpen((open) => !open)}
              type="button"
            >
              <Icon name="menu" />
            </button>
            <span className="topbar-context">Web</span>
            <span className="topbar-slash">/</span>
            <span className="topbar-page">{getPageLabel(pathname, detail)}</span>
          </div>

          <div className="topbar-actions">
            <div ref={setAction} />
            <a className="website-button" href="/" rel="noopener" target="_blank">
              <Icon name="language" size={16} />
              <span>Website</span>
            </a>
            <button aria-label="Notifications" className="icon-button notification-button" type="button">
              <Icon name="notifications" />
            </button>
            <Dialog onOpenChange={setUserOpen} open={userOpen}>
              <DialogTrigger className="user-trigger" type="button">
                <span className="user-avatar">{getInitials(fullName(currentUser) || currentUser.email)}</span>
                <span className="user-identity">
                  <strong>{fullName(currentUser) || currentUser.email}</strong>
                  <small>{currentUser.roleName}</small>
                </span>
                <Icon className={`user-chevron${userOpen ? ' is-open' : ''}`} name="chevron-down" size={14} />
              </DialogTrigger>
              <DialogContent className="admin-user-dialog">
                <DialogHeader>
                  <DialogTitle>Account</DialogTitle>
                  <DialogDescription>{fullName(currentUser) || currentUser.email} · {currentUser.roleName}</DialogDescription>
                </DialogHeader>
                <div className="admin-user-dialog__links">
                  <Link className="admin-user-dialog__link" href="/admin/settings/user">
                    <Icon name="users" size={17} />
                    <span><strong>My profile</strong><small>Account and preferences</small></span>
                  </Link>
                  {permissions.includes(areaPermissions.company) ? (
                    <Link className="admin-user-dialog__link" href="/admin/settings/company">
                      <Icon name="settings" size={17} />
                      <span><strong>Company profile</strong><small>Basic company information</small></span>
                    </Link>
                  ) : null}
                </div>
                <DialogFooter className="admin-user-dialog__footer">
                  <form action={logoutAction}>
                    <Button className="admin-user-dialog__signout" type="submit" variant="destructive">
                      <Icon name="logout" size={17} />
                      Sign out
                    </Button>
                  </form>
                </DialogFooter>
              </DialogContent>
            </Dialog>
          </div>
        </header>

        <div className="admin-content">
          {latestClosedConversation && (
            <Link className="admin-notification" href={`/admin/inbox/${latestClosedConversation.id}`} role="status">
              <span className="admin-notification-icon"><Icon name="notifications" size={18} /></span>
              <span className="admin-notification-copy">
                <strong>Conversation closed</strong>
                <small>A conversation is ready for history review.</small>
              </span>
              <span className="admin-notification-time">{formatNotificationTime(latestClosedConversation.closedAt)}</span>
            </Link>
          )}
          <TopbarSlots.Provider value={{ action }}>{children}</TopbarSlots.Provider>
        </div>
      </main>
    </div>
  );
}

function SidebarItem({ item, isActive, onNavigate }: { item: NavItem; isActive: boolean; onNavigate: () => void }) {
  return (
    <Link
      aria-current={isActive ? 'page' : undefined}
      className={`nav-link${isActive ? ' is-active' : ''}`}
      href={item.href}
      onClick={onNavigate}
    >
      <span className="nav-icon"><Icon name={item.icon} /></span>
      <span className="nav-label">{item.label}</span>
    </Link>
  );
}

function isActivePath(pathname: string, href: string) {
  return href === '/admin' ? pathname === href : pathname === href || pathname.startsWith(`${href}/`);
}

function getPageLabel(pathname: string, detail: string | null) {
  if (pathname === '/admin') {
    return 'Dashboard';
  }
  if (pathname.startsWith('/admin/settings/company')) {
    return 'Company Profile';
  }
  if (pathname.startsWith('/admin/settings/user')) {
    return 'User Profile';
  }
  if (pathname.startsWith('/admin/settings/roles')) {
    return 'Roles';
  }
  if (pathname.startsWith('/admin/settings/github')) {
    return 'GitHub Repository';
  }
  if (pathname.startsWith('/admin/email-templates')) {
    return 'Email Templates';
  }

  const resourceKey = pathname.split('/')[2] ?? '';
  const resource = getResource(resourceKey);
  if (resource) {
    return pathname.split('/').length > 3
      ? `${resource.label} / ${pathname.split('/')[3] === 'new' ? `New ${resource.singularLabel}` : detail ?? 'Details'}`
      : resource.label;
  }

  return 'Admin';
}

function getInitials(value: string) {
  const words = value.trim().split(/\s+/).filter(Boolean);
  return words.slice(0, 2).map((word) => word[0]?.toUpperCase() || '').join('') || '?';
}

function formatNotificationTime(value: string) {
  return new Date(value).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}
