'use client';
import { Card } from '@/Components/Admin/card';

import Link from 'next/link';
import { useCallback, useEffect, useRef, useState } from 'react';
import type { AdminResource, ResourceSort } from '@/Components/admin-data';
import type { SortDirection } from '@/src/api/types';
import { Icon } from '@/Components/icons';
import { ResourceTable } from '@/Components/Admin/resource-table';
import { InfiniteScroll } from '@/Components/Admin/infinite-scroll';
import { Input } from '@/Components/ui/input';
import { NativeSelect, NativeSelectOption } from '@/Components/ui/native-select';

const searchDebounceMs = 300;

export function ResourceList({ resource }: { resource: AdminResource }) {
  const [records, setRecords] = useState(resource.records);
  const [loadedPage, setLoadedPage] = useState(resource.page);
  const [total, setTotal] = useState(resource.total);
  const [totalPages, setTotalPages] = useState(resource.totalPages);
  const [loading, setLoading] = useState(false);
  const [query, setQuery] = useState('');
  const [sort, setSort] = useState<ResourceSort | undefined>(resource.sorts?.[0]);
  const [direction, setDirection] = useState<SortDirection>('Descending');
  const requestId = useRef(0);
  const sortOptions = (resource.sorts ?? []).flatMap((sortField) =>
    (['Descending', 'Ascending'] as const).map((sortDirection) => ({
      key: `${sortField}|${sortDirection}`,
      sort: sortField,
      direction: sortDirection,
      label: `${sortField.replace(/([a-z])([A-Z])/g, '$1 $2')} ${sortDirection === 'Ascending' ? '(asc)' : '(desc)'}`,
    })),
  );

  useEffect(() => {
    setRecords(resource.records);
    setLoadedPage(resource.page);
    setTotal(resource.total);
    setTotalPages(resource.totalPages);
    setLoading(false);
  }, [resource.key, resource.page, resource.records, resource.total, resource.totalPages]);

  // Every narrowing runs in the API. Filtering the loaded pages here would hide matches that live
  // on a page nobody has scrolled to yet.
  const load = useCallback(async (page: number, append: boolean) => {
    const parameters = new URLSearchParams({ page: String(page) });
    if (query.trim()) parameters.set('search', query.trim());
    if (sort) {
      parameters.set('sort', sort);
      parameters.set('direction', direction);
    }

    const id = ++requestId.current;
    setLoading(true);
    try {
      const response = await fetch(
        `/api/admin/resources/${encodeURIComponent(resource.key)}?${parameters}`,
        { cache: 'no-store' },
      );
      if (!response.ok) throw new Error('Resource page failed.');
      const next = await response.json() as AdminResource;
      // A slower earlier keystroke must not overwrite the answer to a later one.
      if (id !== requestId.current) return;

      setRecords((current) => append ? [...current, ...next.records] : next.records);
      setLoadedPage(next.page);
      setTotal(next.total);
      setTotalPages(next.totalPages);
    } finally {
      if (id === requestId.current) setLoading(false);
    }
  }, [direction, query, resource.key, sort]);

  const isFirstRender = useRef(true);
  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }

    const timer = setTimeout(() => void load(1, false), searchDebounceMs);
    return () => clearTimeout(timer);
  }, [load]);

  return (
    <section aria-labelledby="resource-title" className="resource-page">
      <h1 className="visually-hidden" id="resource-title">{resource.label}</h1>

      <Card className="list-toolbar">
        <label className="search-field" htmlFor={`${resource.key}-search`}>
          <Icon name="search" size={17} />
          <span className="visually-hidden">Search {resource.label.toLowerCase()}</span>
          <Input
            id={`${resource.key}-search`}
            onChange={(event) => setQuery(event.target.value)}
            placeholder={`Search ${resource.label.toLowerCase()}`}
            type="search"
            value={query}
          />
        </label>
        {sortOptions.length > 0 ? (
          <label className="filter-field" htmlFor={`${resource.key}-sort`}>
            <Icon name="filter" size={16} />
            <span className="visually-hidden">Sort {resource.label.toLowerCase()}</span>
            <NativeSelect
              className="w-full"
              id={`${resource.key}-sort`}
              onChange={(event) => {
                const option = sortOptions.find((candidate) => candidate.key === event.target.value);
                if (!option) return;

                setSort(option.sort);
                setDirection(option.direction);
              }}
              value={`${sort}|${direction}`}
            >
              {sortOptions.map((option) => (
                <NativeSelectOption key={option.key} value={option.key}>{option.label}</NativeSelectOption>
              ))}
            </NativeSelect>
          </label>
        ) : null}
        <div className="resource-toolbar-actions">
          <span className="toolbar-count">{records.length} of {total}</span>
          {resource.canCreate ? (
            <Link className="button button-primary button-small" href={`/admin/${resource.key}/new`}>
              <Icon name="plus" size={15} />
              New {resource.singularLabel}
            </Link>
          ) : null}
        </div>
      </Card>

      <Card className="table-card">
        {records.length > 0 ? (
          <ResourceTable resource={resource} records={records} />
        ) : (
          <div className="empty-state">
            <span className="empty-state-icon"><Icon name="search" size={22} /></span>
            <h2 className="section-title">{query ? 'No matches' : 'No records returned'}</h2>
            <p>{query ? 'Try a different search term.' : 'The API returned an empty list.'}</p>
          </div>
        )}
        <div className="table-footer">
          <span>Showing {records.length} of {total} {resource.label.toLowerCase()} through page {loadedPage}.</span>
          {loadedPage < totalPages ? (
            <span className="table-footer-note">Scroll to load more.</span>
          ) : (
            <span className="table-footer-note">Select a row to open its live details.</span>
          )}
        </div>
        <InfiniteScroll
          hasMore={loadedPage < totalPages}
          loading={loading}
          onLoadMore={() => {
            if (!loading && loadedPage < totalPages) void load(loadedPage + 1, true);
          }}
        />
      </Card>
    </section>
  );
}
