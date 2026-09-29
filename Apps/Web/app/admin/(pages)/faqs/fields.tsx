import type { EditorValue } from '@/Components/admin-data';
import { TextField, TextAreaField, SelectField } from '@/Components/Admin/form-fields';

export function FaqsFields({ values, isNew }: { values?: Record<string, EditorValue>; isNew: boolean }) {
  return (<>
          {!isNew && <input name="key" type="hidden" value={String(values?.key || '')} />}
          <TextField label="Key" name="key" value={values?.key} disabled={!isNew} required />
          <TextField label="Category" name="category" value={values?.category} required />
          <TextField label="Sort order" name="sortOrder" value={values?.sortOrder || '0'} type="number" />
          <TextField label="Question" name="question" value={values?.question} required wide />
          <TextAreaField label="Answer" name="answer" value={values?.answer} required wide />
          <TextAreaField label="SEO description" name="seoDescription" value={values?.seoDescription} required wide />
          <TextField label="Publication date and time (UTC)" name="publishedAt" value={values?.publishedAt} type="datetime-local" />
          <SelectField label="Publication status" name="isDraft" value={booleanValue(values?.isDraft) ? 'true' : 'false'} options={['false', 'true']} optionLabels={['Published', 'Draft']} wide />
        </>);
}

function booleanValue(value: EditorValue | undefined) {
  return value === true || value === 'true';
}

