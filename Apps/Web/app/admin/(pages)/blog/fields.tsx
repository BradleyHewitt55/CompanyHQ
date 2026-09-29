import type { EditorValue } from '@/Components/admin-data';
import { TextField, TextAreaField, SelectField } from '@/Components/Admin/form-fields';

export function BlogFields({ values, isNew }: { values?: Record<string, EditorValue>; isNew: boolean }) {
  return (<>
          {!isNew && <input name="slug" type="hidden" value={String(values?.slug || '')} />}
          <TextField label="Slug" name="slug" value={values?.slug} disabled={!isNew} required />
          <TextField label="Title" name="title" value={values?.title} required />
          <TextField label="Author" name="author" value={values?.author} required />
          <TextField label="Category" name="category" value={values?.category} required />
          <TextField label="Cover image URL" name="coverImageUrl" value={values?.coverImageUrl} />
          <TextField label="Publication date and time (UTC)" name="publishedAt" value={values?.publishedAt} type="datetime-local" />
          <TextField label="Sort order" name="sortOrder" value={values?.sortOrder || '0'} type="number" />
          <TextAreaField label="Description" name="description" value={values?.description} required wide />
          <TextAreaField label="Article HTML" name="body" value={values?.body} required wide />
          <SelectField label="Publication status" name="isDraft" value={booleanValue(values?.isDraft) ? 'true' : 'false'} options={['false', 'true']} optionLabels={['Published', 'Draft']} wide />
        </>);
}

function booleanValue(value: EditorValue | undefined) {
  return value === true || value === 'true';
}

