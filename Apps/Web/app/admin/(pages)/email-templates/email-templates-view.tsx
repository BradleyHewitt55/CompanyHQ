'use client';

import { useActionState, useEffect, useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { Card } from '@/Components/Admin/card';
import { Icon } from '@/Components/icons';
import type { AdminEmailTemplateDto } from '../../../../src/api/types';
import {
  previewEmailTemplateAction,
  resetEmailTemplateAction,
  saveEmailTemplateAction,
  type EmailTemplateActionState,
} from './actions';

const initialActionState: EmailTemplateActionState = { error: '', success: '' };

export function EmailTemplatesView({ templates }: { templates: AdminEmailTemplateDto[] }) {
  const [selectedKey, setSelectedKey] = useState(templates[0]?.key ?? '');
  const [html, setHtml] = useState(templates[0]?.html ?? '');
  const [previewHtml, setPreviewHtml] = useState('');
  const [previewSubject, setPreviewSubject] = useState('');
  const [previewError, setPreviewError] = useState('');
  const [isPreviewing, startPreview] = useTransition();
  const router = useRouter();
  const [saveState, saveAction, savePending] = useActionState(saveEmailTemplateAction, initialActionState);
  const [resetState, resetAction, resetPending] = useActionState(resetEmailTemplateAction, initialActionState);
  const selected = templates.find((template) => template.key === selectedKey) ?? templates[0];

  useEffect(() => {
    if (!selected) return;
    setSelectedKey(selected.key);
    setHtml(selected.html);
    setPreviewHtml('');
    setPreviewSubject('');
    setPreviewError('');
  }, [selectedKey, selected?.html]);

  useEffect(() => {
    if (resetState.success) {
      router.refresh();
    }
  }, [resetState.success, router]);

  useEffect(() => {
    if (saveState.success) {
      router.refresh();
    }
  }, [router, saveState.success]);

  if (!selected) {
    return (
      <section className="email-template-page" aria-labelledby="email-template-title">
        <h1 id="email-template-title" className="visually-hidden">Email templates</h1>
        <Card className="api-empty-card"><p>No controlled templates are available.</p></Card>
      </section>
    );
  }

  const chooseTemplate = (key: string) => {
    const next = templates.find((template) => template.key === key);
    if (!next) return;
    setSelectedKey(next.key);
    setHtml(next.html);
  };

  const preview = () => {
    const formData = new FormData();
    formData.set('key', selected.key);
    formData.set('html', html);
    formData.set('values', JSON.stringify(sampleValues(selected)));
    startPreview(async () => {
      const result = await previewEmailTemplateAction(formData);
      setPreviewError(result.error);
      setPreviewHtml(result.preview?.html ?? '');
      setPreviewSubject(result.preview?.subject ?? '');
    });
  };

  return (
    <section className="email-template-page" aria-labelledby="email-template-title">
      <div className="email-template-heading">
        <div>
          <h1 id="email-template-title" className="visually-hidden">Email templates</h1>
          <p className="section-description">Edit the HTML used by transactional email. Changes apply to the next send.</p>
        </div>
        <Icon name="envelope" size={24} />
      </div>

      <div className="email-template-layout">
        <Card className="email-template-list">
          <h2 className="card-title">Controlled templates</h2>
          <div className="email-template-list-items">
            {templates.map((template) => (
              <button
                className={`email-template-list-item${template.key === selected.key ? ' is-selected' : ''}`}
                key={template.key}
                onClick={() => chooseTemplate(template.key)}
                type="button"
              >
                <strong>{template.displayName}</strong>
                <span>{template.key}</span>
                <small>{template.isCustomized ? 'Customized' : 'Packaged default'}</small>
              </button>
            ))}
          </div>
        </Card>

        <div className="email-template-editor-column">
          <Card className="email-template-editor-card">
            <div className="card-header">
              <div>
                <h2 className="card-title">{selected.displayName}</h2>
                <p className="email-template-key">{selected.key}</p>
              </div>
              <span className={`email-template-status${selected.isCustomized ? ' is-customized' : ''}`}>
                {selected.isCustomized ? 'Customized' : 'Packaged default'}
              </span>
            </div>
            <div className="card-body">
              {saveState.error || resetState.error || previewError ? (
                <div className="form-error" role="alert">{saveState.error || resetState.error || previewError}</div>
              ) : null}
              {saveState.success || resetState.success ? (
                <div className="save-notice" role="status"><Icon name="check" size={16} /> {saveState.success || resetState.success}</div>
              ) : null}
              <form action={saveAction}>
                <input name="key" type="hidden" value={selected.key} />
                <label className="email-template-html-label" htmlFor="email-template-html">HTML source</label>
                <textarea
                  aria-describedby="email-template-help"
                  className="email-template-html"
                  id="email-template-html"
                  name="html"
                  onChange={(event) => setHtml(event.target.value)}
                  spellCheck={false}
                  value={html}
                />
                <p className="email-template-help" id="email-template-help">Values are escaped by the renderer. Scripts, event handlers, and javascript URLs are rejected.</p>
                <div className="form-actions">
                  <button className="button button-primary" disabled={savePending} type="submit">
                    <Icon name="check" size={16} /> {savePending ? 'Saving...' : 'Save template'}
                  </button>
                  <button className="button button-secondary" disabled={isPreviewing} onClick={preview} type="button">
                    <Icon name="eye-slash" size={16} /> {isPreviewing ? 'Rendering...' : 'Preview'}
                  </button>
                </div>
              </form>
              <form action={resetAction} onSubmit={(event) => {
                if (!window.confirm('Reset this template to its packaged default?')) event.preventDefault();
              }}>
                <input name="key" type="hidden" value={selected.key} />
                <button className="email-template-reset" disabled={resetPending || !selected.isCustomized} type="submit">
                  {resetPending ? 'Resetting...' : 'Reset to packaged default'}
                </button>
              </form>
            </div>
          </Card>

          <Card className="email-template-meta-card">
            <div className="card-header"><h2 className="card-title">Supported placeholders</h2></div>
            <div className="card-body email-template-placeholders">
              {selected.supportedPlaceholders.map((placeholder) => (
                <code key={placeholder}>{`{{${placeholder}}}`}{selected.requiredPlaceholders.includes(placeholder) ? ' required' : ''}</code>
              ))}
              {selected.supportedPlaceholders.length === 0 ? <span>No dynamic values.</span> : null}
            </div>
          </Card>

          <Card className="email-template-preview-card">
            <div className="card-header">
              <div>
                <h2 className="card-title">Safe preview</h2>
                {previewSubject ? <p className="email-template-preview-subject">Subject: {previewSubject}</p> : null}
              </div>
            </div>
            <div className="email-template-preview-frame">
              {previewHtml ? (
                <iframe title={`${selected.displayName} preview`} sandbox="" srcDoc={previewHtml} />
              ) : (
                <p>Preview uses sample values and never sends an email.</p>
              )}
            </div>
          </Card>
        </div>
      </div>
    </section>
  );
}

function sampleValues(template: AdminEmailTemplateDto) {
  return Object.fromEntries(template.supportedPlaceholders.map((placeholder) => [
    placeholder,
    placeholder === 'link'
      ? 'https://example.com/example'
      : placeholder === 'password'
        ? 'TemporaryPassword123!'
        : placeholder === 'message'
          ? 'This is a safe preview message.'
          : `Sample ${placeholder}`,
  ]));
}
