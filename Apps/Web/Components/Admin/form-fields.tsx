import type { ChangeEventHandler } from 'react';
import type { EditorValue } from '../admin-data';
import { Input } from '@/Components/ui/input';
import { NativeSelect, NativeSelectOption } from '@/Components/ui/native-select';
import { Textarea } from '@/Components/ui/textarea';
export function TextField({
  label,
  name,
  value,
  type = 'text',
  required = false,
  disabled = false,
  wide = false,
}: {
  label: string;
  name: string;
  value: EditorValue | undefined;
  type?: string;
  required?: boolean;
  disabled?: boolean;
  wide?: boolean;
}) {
  return (
    <label className={`form-field${wide ? ' form-field-wide' : ''}`} htmlFor={name}>
      <span>{label}{required ? ' *' : ''}</span>
      <Input defaultValue={textValue(value)} disabled={disabled} id={name} name={name} required={required} type={type} />
    </label>
  );
}

export function TextAreaField({
  label,
  name,
  value,
  required = false,
  disabled = false,
  wide = false,
}: {
  label: string;
  name: string;
  value: EditorValue | undefined;
  required?: boolean;
  disabled?: boolean;
  wide?: boolean;
}) {
  return (
    <label className={`form-field${wide ? ' form-field-wide' : ''}`} htmlFor={name}>
      <span>{label}{required ? ' *' : ''}</span>
      <Textarea defaultValue={textValue(value)} disabled={disabled} id={name} name={name} required={required} rows={5} />
    </label>
  );
}

// Generic over the option type so a caller passes the API's own union (UserRole, SortDirection, ...)
// and a contract change becomes a compile error here instead of a broken select at runtime.
export function SelectField<TOption extends string>({
  label,
  name,
  value,
  options,
  optionLabels = options,
  onChange,
  wide = false,
}: {
  label: string;
  name: string;
  value: EditorValue | undefined;
  options: readonly TOption[];
  optionLabels?: readonly string[];
  onChange?: ChangeEventHandler<HTMLSelectElement>;
  wide?: boolean;
}) {
  return (
    <label className={`form-field${wide ? ' form-field-wide' : ''}`} htmlFor={name}>
      <span>{label}</span>
      <NativeSelect className="w-full" defaultValue={textValue(value) || options[0]} id={name} name={name} onChange={onChange}>
        {options.map((option, index) => <NativeSelectOption key={option} value={option}>{optionLabels[index] || option}</NativeSelectOption>)}
      </NativeSelect>
    </label>
  );
}

function textValue(value: EditorValue | undefined) {
  return typeof value === 'boolean' ? String(value) : value || '';
}
