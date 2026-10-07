'use client';
import { useRouter } from 'next/navigation';
import { Fragment, useActionState, useEffect, useId, useRef, useState } from 'react';
import { updateCompanyProfileAction, updateProfileAction, type AdminActionState } from '@/app/admin/actions';
import { CompanyLogo } from '@/Components/company-logo';
import { Icon } from '@/Components/icons';
import type { AdminGitHubConnectionDto, AuthUserDto, ClientCompanyProfileDto, ClientCompanyEmailDto, ClientUserDto } from '@/src/api/types';
import { TextField, TextAreaField } from '@/Components/Admin/form-fields';
import { Button } from '@/Components/ui/button';
import { Input } from '@/Components/ui/input';
import { Textarea } from '@/Components/ui/textarea';
import type { EditorValue } from '@/Components/admin-data';
import { TopbarAction } from '@/app/admin/topbar-slots';
import { DeleteAccountCard } from './delete-account';
const initialActionState: AdminActionState = { error: '', success: '' };

export function SettingsPage({
  section,
  currentUser,
  profile,
  company,
  github,
  githubStatus,
}: {
  section: 'company' | 'user';
  currentUser: AuthUserDto;
  profile?: ClientUserDto;
  company: ClientCompanyProfileDto | null;
  /** Present only for a role that may connect the bug-report repository. */
  github?: AdminGitHubConnectionDto;
  githubStatus?: string;
}) {
  const isCompany = section === 'company';
  const router = useRouter();
  const [isEditing, setIsEditing] = useState(false);
  const [draftValues, setDraftValues] = useState<Record<string, EditorValue>>();
  const [draftVersion, setDraftVersion] = useState(0);
  const [state, formAction, pending] = useActionState(
    isCompany ? updateCompanyProfileAction : updateProfileAction,
    initialActionState,
  );

  useEffect(() => {
    if (state.formValues) {
      setDraftValues(state.formValues);
      setDraftVersion((value) => value + 1);
      return;
    }

    if (state.success) {
      setDraftValues(undefined);
      setIsEditing(false);
      router.refresh();
    }
  }, [router, state.formValues, state.success]);

  const cancelEditing = () => {
    setDraftValues(undefined);
    setIsEditing(false);
  };
  const fieldKey = isEditing ? `editing-${draftVersion}` : 'viewing';

  if (isCompany) {
    return (
      <section aria-labelledby="settings-title" className="settings-page">
        {isEditing && <TopbarAction><button className="button button-secondary" onClick={cancelEditing} type="button">Cancel</button></TopbarAction>}
        <div className="detail-grid">
          <div>
            <h1 className="visually-hidden" id="settings-title">Company Profile</h1>
            <form action={formAction} aria-busy={pending} className={`form-card settings-form-card${isEditing ? ' is-editing' : ''}`} id="settings-form">
              <div className="card-header">
                <h2 className="card-title">Company information</h2>
                <span className="form-required">Required fields marked *</span>
              </div>
              <div className="card-body form-body">
                {state.error ? <div className="form-error" role="alert">{state.error}</div> : null}
                {state.success ? <div className="save-notice" role="status"><Icon name="check" size={16} /> {state.success}</div> : null}
                {githubStatus === 'linked' ? <div className="save-notice" role="status">GitHub repository linked. Widget reports now create issues there.</div> : null}
                {githubStatus === 'unconfigured' ? <div className="form-error" role="alert">Set GITHUB_CLIENT_ID, GITHUB_CLIENT_SECRET, and GITHUB_CALLBACK_URL on the Web server first.</div> : null}
                {githubStatus === 'origin' ? <div className="form-error" role="alert">GitHub sign-in stopped because the admin address and GITHUB_CALLBACK_URL use different hosts. Set the callback to the admin host, update the GitHub App redirect URI, restart Web, then connect again.</div> : null}
                {githubStatus === 'install' ? <div className="form-error" role="alert">The GitHub App is authorized but not installed on your GitHub account. Install it with access to the repository you want to link, then connect again.</div> : null}
                {githubStatus === 'permission' ? <div className="form-error" role="alert">The GitHub App needs Issues read and write permission for the repository you want to link. Update its GitHub App permissions, then connect again.</div> : null}
                {githubStatus === 'expiring' ? <div className="form-error" role="alert">This GitHub App uses expiring user tokens, which this connection cannot renew. Turn off token expiration in the app settings, then reconnect.</div> : null}
                {githubStatus === 'failed' || githubStatus === 'expired' ? <div className="form-error" role="alert">GitHub connection failed or expired. Connect again.</div> : null}
                {!company ? <div className="api-note" role="status">No company profile is stored yet. Fill these in and save to create it.</div> : null}
                {!isEditing ? <div className="view-mode-note"><Icon name="edit" size={15} /> Select Edit to make changes.</div> : null}
                <fieldset className="editor-fields" disabled={!isEditing}>
                  <div className="field-grid">
                    <div className="company-identity">
                      <Fragment key={`company-name-${fieldKey}`}>
                        <TextField label="Company name" name="name" value={draftValues?.name ?? company?.name ?? ''} required />
                      </Fragment>
                      <LogoUploadField
                        companyName={draftValues && typeof draftValues.name === 'string' ? draftValues.name : company?.name || 'Company'}
                        isEditing={isEditing}
                        logoUrl={draftValues && typeof draftValues.logoUrl === 'string' ? draftValues.logoUrl : company?.logoUrl || ''}
                      />
                    </div>
                    <Fragment key={`company-fields-${fieldKey}`}>
                      <TextField label="Website" name="website" value={draftValues?.website ?? company?.website ?? ''} required />
                      <PublicEmailsFields key={`emails-${fieldKey}`} company={company} draftValues={draftValues} isEditing={isEditing} />
                      <TextField label="Facebook page username" name="facebookAccount" value={draftValues?.facebookAccount ?? company?.facebookAccount ?? ''} />
                      <TextField label="Instagram username" name="instagramAccount" value={draftValues?.instagramAccount ?? company?.instagramAccount ?? ''} />
                      <TextField label="WhatsApp number (international format)" name="whatsAppNumber" type="tel" value={draftValues?.whatsAppNumber ?? company?.whatsAppNumber ?? ''} />
                      <TextAreaField label="Short description" name="description" value={draftValues?.description ?? company?.description ?? ''} required wide />
                    </Fragment>
                  </div>
                </fieldset>
                {github ? (
                  <div className="github-link-setting">
                    <div><strong>Bug report repository</strong><p>{github.repository || 'No GitHub repository linked yet.'}</p></div>
                    <a className="button button-secondary" href="/api/admin/github/start">{github.repository ? 'Change repository' : 'Connect GitHub'}</a>
                  </div>
                ) : null}
              </div>
            </form>
          </div>
          <SettingsAside isEditing={isEditing} onEdit={() => setIsEditing(true)} pending={pending} />
        </div>
      </section>
    );
  }

  const firstName = draftValues?.firstName ?? profile?.firstName ?? currentUser.firstName ?? '';
  const lastName = draftValues?.lastName ?? profile?.lastName ?? currentUser.lastName ?? '';
  const bio = draftValues?.bio ?? profile?.bio ?? '';
  const profileImageUrl = draftValues?.profileImageUrl ?? profile?.profileImageUrl ?? '';

  return (
      <section aria-labelledby="settings-title" className="settings-page">
        {isEditing && <TopbarAction><button className="button button-secondary" onClick={cancelEditing} type="button">Cancel</button></TopbarAction>}
        <div className="detail-grid">
          <div>
            <h1 className="visually-hidden" id="settings-title">User Profile</h1>
            <form action={formAction} aria-busy={pending} className={`form-card settings-form-card${isEditing ? ' is-editing' : ''}`} id="settings-form">
              <div className="card-header">
                <h2 className="card-title">Account information</h2>
                <span className="form-required">Required fields marked *</span>
              </div>
              <div className="card-body form-body">
                {state.error ? <div className="form-error" role="alert">{state.error}</div> : null}
                {state.success ? <div className="save-notice" role="status"><Icon name="check" size={16} /> {state.success}</div> : null}
                {!isEditing ? <div className="view-mode-note"><Icon name="edit" size={15} /> Select Edit to make changes.</div> : null}
                <fieldset className="editor-fields" disabled={!isEditing}>
                  <div className="field-grid">
                    <Fragment key={fieldKey}>
                      <TextField label="First name" name="firstName" value={firstName} required />
                      <TextField label="Last name" name="lastName" value={lastName} required />
                      <TextField label="Email address" name="email" value={currentUser.email} disabled />
                      <TextField label="Role" name="role" value={currentUser.roleName} disabled />
                      <TextField label="Profile image URL" name="profileImageUrl" value={profileImageUrl} />
                      <TextAreaField label="Bio" name="bio" value={bio} wide />
                    </Fragment>
                  </div>
                </fieldset>
              </div>
            </form>
            <DeleteAccountCard fullName={[firstName, lastName].filter(Boolean).join(' ') || currentUser.email} />
          </div>
          <SettingsAside isEditing={isEditing} onEdit={() => setIsEditing(true)} pending={pending} />
        </div>
      </section>
    );
}

function SettingsAside({ isEditing, onEdit, pending }: {
  isEditing: boolean;
  onEdit: () => void;
  pending: boolean;
}) {
  return (
    <aside className="editor-aside">
      <div className="editor-aside__actions">
        {!isEditing ? (
          <button className="edit-button" onClick={onEdit} type="button">
            <Icon name="edit" size={16} />
            Edit
          </button>
        ) : null}
        {isEditing ? (
          <>
            <button className="button button-primary" disabled={pending} form="settings-form" type="submit">
              <Icon name="check" size={17} />
              {pending ? 'Saving...' : 'Save changes'}
            </button>
          </>
        ) : null}
      </div>
    </aside>
  );
}

function LogoUploadField({
  companyName,
  isEditing,
  logoUrl,
}: {
  companyName: string;
  isEditing: boolean;
  logoUrl: string;
}) {
  const inputId = useId();
  const input = useRef<HTMLInputElement>(null);
  const objectUrl = useRef<string | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState(logoUrl);
  const [removeCurrent, setRemoveCurrent] = useState(false);

  useEffect(() => () => {
    if (objectUrl.current) URL.revokeObjectURL(objectUrl.current);
  }, []);

  useEffect(() => {
    if (isEditing) return;
    if (objectUrl.current) URL.revokeObjectURL(objectUrl.current);
    objectUrl.current = null;
    setSelectedFile(null);
    setPreviewUrl(logoUrl);
    setRemoveCurrent(false);
  }, [isEditing, logoUrl]);

  function clearObjectUrl() {
    if (objectUrl.current) URL.revokeObjectURL(objectUrl.current);
    objectUrl.current = null;
  }

  function choose(file: File | undefined) {
    clearObjectUrl();
    setRemoveCurrent(false);
    setSelectedFile(file || null);
    if (file) {
      objectUrl.current = URL.createObjectURL(file);
      setPreviewUrl(objectUrl.current);
    } else {
      setPreviewUrl(logoUrl);
    }
  }

  function removeLogo() {
    if (input.current) input.current.value = '';
    clearObjectUrl();
    setSelectedFile(null);
    setPreviewUrl('');
    setRemoveCurrent(true);
  }

  function keepLogo() {
    setPreviewUrl(logoUrl);
    setRemoveCurrent(false);
  }

  const currentFileName = logoUrl.split('/').pop() || 'company-logo';

  return (
    <div className="form-field logo-field">
      <div className="logo-field__heading">
        <span id="company-logo-label">Logo image</span>
        <span className="logo-field__eyebrow">Brand asset</span>
      </div>
      <p className="logo-field__description">Shown in the admin console and on the public site.</p>
      <div aria-label="Company logo files" className="logo-card-list" role="list">
        {previewUrl ? (
          <div className="logo-card" role="listitem">
            <span className="logo-card__preview"><CompanyLogo alt={`${companyName} logo`} className="logo-card__image" isSvg={selectedFile?.type === 'image/svg+xml' || selectedFile?.name.toLowerCase().endsWith('.svg')} src={previewUrl} /></span>
            <span className="logo-card__details">
              <strong>{selectedFile?.name || currentFileName}</strong>
              <small>{selectedFile ? `${formatFileSize(selectedFile.size)}, ready to save` : 'Published company logo'}</small>
            </span>
            <Icon className="logo-card__status" name="check2-circle" size={18} />
          </div>
        ) : (
          <div className="logo-empty-card" role="listitem">
            <Icon name="images" size={20} />
            <span>{removeCurrent ? 'The default logo will be used.' : 'No custom logo selected yet.'}</span>
          </div>
        )}
      </div>
      <div className="logo-upload-row">
        <Button aria-controls={inputId} disabled={!isEditing} onClick={() => { if (input.current) input.current.value = ''; input.current?.click(); }} size="sm" type="button" variant="outline">
          <Icon name="images" size={15} />
          {previewUrl ? 'Replace logo' : 'Choose logo'}
        </Button>
        <small id={`${inputId}-hint`}>SVG, PNG, JPEG, or WebP. Maximum 5 MB.</small>
      </div>
      <Input
        accept="image/svg+xml,image/png,image/jpeg,image/webp"
        aria-describedby={`${inputId}-hint`}
        aria-labelledby="company-logo-label"
        className="visually-hidden"
        disabled={!isEditing}
        id={inputId}
        name="logo"
        onChange={event => choose(event.currentTarget.files?.[0])}
        ref={input}
        type="file"
      />
      <input name="logoUrl" type="hidden" value={logoUrl} />
      {logoUrl ? (
        <>
          <input checked={removeCurrent} className="visually-hidden" name="removeLogo" onChange={event => setRemoveCurrent(event.currentTarget.checked)} type="checkbox" />
          <Button className="logo-remove" disabled={!isEditing} onClick={removeCurrent ? keepLogo : removeLogo} size="sm" type="button" variant="ghost">
            {removeCurrent ? 'Keep current logo' : 'Remove current logo'}
          </Button>
        </>
      ) : null}
    </div>
  );
}

function formatFileSize(size: number) {
  return `${(size / 1024 / 1024).toFixed(2)} MB`;
}

function PublicEmailsFields({ company, draftValues, isEditing }: {
  company: ClientCompanyProfileDto | null;
  draftValues?: Record<string, EditorValue>;
  isEditing: boolean;
}) {
  const [emails, setEmails] = useState<ClientCompanyEmailDto[]>(() => {
    if (draftValues?.publicEmailCount) {
      return Array.from({ length: Math.min(20, Math.max(2, Number(draftValues.publicEmailCount))) }, (_, index) => ({
        title: String(draftValues[`publicEmailTitle_${index}`] ?? ''),
        address: String(draftValues[`publicEmailAddress_${index}`] ?? ''),
        description: String(draftValues[`publicEmailDescription_${index}`] ?? ''),
      }));
    }
    return company?.publicEmails?.length ? [...company.publicEmails] : [
      { title: 'Support', address: company?.supportEmail ?? '', description: '' },
      { title: 'Information', address: company?.infoEmail ?? '', description: '' },
    ];
  });
  const update = (index: number, field: keyof ClientCompanyEmailDto, value: string) =>
    setEmails(current => current.map((email, at) => at === index ? { ...email, [field]: value } : email));
  return <div className="form-field form-field-wide">
    <input name="publicEmailCount" type="hidden" value={emails.length} />
    <h3>Public email addresses</h3>
    {emails.map((email, index) => <div className="field-grid" key={index}>
      <label className="form-field" htmlFor={`publicEmailTitle_${index}`}>
        <span>{index === 0 ? 'Support title' : index === 1 ? 'Information title' : 'Title'} *</span>
        <Input id={`publicEmailTitle_${index}`} name={`publicEmailTitle_${index}`} value={email.title} onChange={event => update(index, 'title', event.target.value)} maxLength={160} required />
      </label>
      <label className="form-field" htmlFor={`publicEmailAddress_${index}`}>
        <span>Email address *</span>
        <Input id={`publicEmailAddress_${index}`} name={`publicEmailAddress_${index}`} type="email" value={email.address} onChange={event => update(index, 'address', event.target.value)} maxLength={320} required />
      </label>
      <label className="form-field form-field-wide" htmlFor={`publicEmailDescription_${index}`}>
        <span>Description</span>
        <Textarea id={`publicEmailDescription_${index}`} name={`publicEmailDescription_${index}`} value={email.description ?? ''} onChange={event => update(index, 'description', event.target.value)} maxLength={2000} rows={2} />
      </label>
      {isEditing && index > 1 && <Button type="button" variant="outline" onClick={() => setEmails(current => current.filter((_, at) => at !== index))}>Remove address</Button>}
    </div>)}
    {isEditing && emails.length < 20 && <Button type="button" variant="outline" onClick={() => setEmails(current => [...current, { title: '', address: '', description: '' }])}>Add email address</Button>}
  </div>;
}
