'use client';

import { useMemo, useState } from 'react';
import type { EditorValue } from '@/Components/admin-data';
import { TextField, TextAreaField } from '@/Components/Admin/form-fields';
import { Input } from '@/Components/ui/input';
import { Checkbox } from '@/Components/ui/checkbox';
import { Field, FieldContent, FieldLabel } from '@/Components/ui/field';
import {
  allMcpCapabilities,
  mcpCapabilityLabels,
  type McpCapability,
} from '@/src/api/mcp-capabilities';

const capabilityGroups: readonly {
  title: string;
  description: string;
  capabilities: readonly McpCapability[];
}[] = [
  {
    title: 'My account and areas',
    description: 'Notification preferences and named-area search.',
    capabilities: ['get_my_notification_preferences', 'search_area'],
  },
  {
    title: 'Published content',
    description: 'Blog and FAQ draft review and authoring, when the saved role grants each action.',
    capabilities: ['list_blog_posts', 'get_blog_post', 'create_blog_post', 'update_blog_post', 'delete_blog_post', 'list_faq_entries', 'get_faq_entry', 'create_faq_entry', 'update_faq_entry', 'delete_faq_entry'],
  },
  {
    title: 'Email templates',
    description: 'Template management and preview, when the saved role grants each action.',
    capabilities: ['list_email_templates', 'get_email_template', 'update_email_template', 'preview_email_template', 'reset_email_template'],
  },
];

export function AccountFields({ values, isNew, isOrganization = false }: {
  values?: Record<string, EditorValue>;
  isNew: boolean;
  isOrganization?: boolean;
}) {
  const initialRoleName = typeof values?.roleName === 'string' && values.roleName
    ? values.roleName : typeof values?.role === 'string' ? values.role : 'Client';
  const initialDisabled = useMemo(() => {
    return parseCapabilities(values?.mcpDisabledTools);
  }, [values?.mcpDisabledTools]);
  const [enabled, setEnabled] = useState<Set<McpCapability>>(() => new Set(
    allMcpCapabilities.filter((capability) => !initialDisabled.has(capability)),
  ));

  const toggle = (capability: McpCapability, checked: boolean) => {
    setEnabled((current) => {
      const next = new Set(current);
      if (checked) next.add(capability);
      else next.delete(capability);
      return next;
    });
  };

  const available = allMcpCapabilities;
  const enabledCount = available.filter((capability) => enabled.has(capability)).length;

  return (<>
          <TextField label={isOrganization ? 'Organization name' : 'First name'} name="firstName" value={values?.firstName} required={isOrganization} />
          {!isOrganization && <TextField label="Last name" name="lastName" value={values?.lastName} />}
          <TextField label="Email address" name="email" value={values?.email} required type="email" wide />
          <TextField label={isNew ? 'Temporary password' : 'New password'} name="password" value="" required={isNew} type="password" />
          <label className="form-field" htmlFor="roleName">
            <span>Role name *</span>
            <Input defaultValue={initialRoleName} id="roleName" maxLength={80} name="roleName" required />
          </label>
          <TextAreaField label="Bio" name="bio" value={values?.bio} wide />
          <fieldset className="form-field form-field-wide mcp-access" aria-describedby="mcp-access-help">
            <div className="mcp-access__heading">
              <div>
                <legend>MCP access</legend>
                <p id="mcp-access-help">Optional restrictions for this account. Grant shared permissions by editing the saved role.</p>
              </div>
              <span>{enabledCount} of {available.length} enabled</span>
            </div>
            <div className="mcp-capability-groups">
              {capabilityGroups.map((group) => <details className="mcp-capability-group" key={group.title}>
                <summary className="mcp-capability-group__heading">
                  <h3 id={`mcp-${group.title.toLowerCase().replaceAll(' ', '-')}`}>{group.title}</h3>
                  <p>{group.description}</p>
                </summary>
                <div className="mcp-capability-list">
                  {group.capabilities.map((capability) => {
                    const id = `mcp-capability-${capability}`;
                    return <Field key={capability} orientation="horizontal">
                      <Checkbox
                        aria-label={mcpCapabilityLabels[capability]}
                        checked={enabled.has(capability)}
                        id={id}
                        name="mcpEnabledTool"
                        onCheckedChange={(checked) => toggle(capability, checked === true)}
                        value={capability}
                      />
                      <FieldContent>
                        <FieldLabel htmlFor={id}>{mcpCapabilityLabels[capability]}</FieldLabel>
                      </FieldContent>
                    </Field>;
                  })}
                </div>
              </details>)}
            </div>
          </fieldset>
        </>);
}

function parseCapabilities(value: EditorValue | undefined) {
  if (typeof value !== 'string' || value.trim() === '') return new Set<McpCapability>();

  try {
    const parsed = JSON.parse(value) as unknown;
    return new Set(
      Array.isArray(parsed)
        ? parsed.filter((capability): capability is McpCapability => allMcpCapabilities.includes(capability as McpCapability))
        : [],
    );
  } catch {
    return new Set<McpCapability>();
  }
}
