import { NextResponse } from 'next/server';

import { getResource } from '../../../../../Components/admin-data';
import { getAdminResource, type AdminResourceQuery } from '../../../../../src/api/admin';
import { ApiError, requestJson } from '../../../../../src/api/client';
import { readAccessToken } from '../../../../../src/api/session';
import type { AuthUserDto } from '../../../../../src/api/types';

export const dynamic = 'force-dynamic';

export async function GET(
  request: Request,
  { params }: { params: Promise<{ resource: string }> },
) {
  if (!await readAccessToken()) {
    return NextResponse.json({ detail: 'Sign in to continue.' }, { status: 401 });
  }

  try {
    const user = await requestJson<AuthUserDto>('/api/auth/me');
    const { resource: resourceKey } = await params;
    const searchParams = new URL(request.url).searchParams;
    const page = Number.parseInt(searchParams.get('page') ?? '1', 10);
    // Sort is matched against what the resource declares, so an arbitrary query value never
    // reaches the API as a sort field.
    const requestedSort = searchParams.get('sort');
    const requestedDirection = searchParams.get('direction');
    const query: AdminResourceQuery = {
      search: searchParams.get('search') ?? undefined,
      sort: getResource(resourceKey)?.sorts?.find((option) => option === requestedSort),
      direction: requestedDirection === 'Ascending' || requestedDirection === 'Descending'
        ? requestedDirection
        : undefined,
    };
    const resource = await getAdminResource(resourceKey, user.permissions, Number.isFinite(page) && page > 0 ? page : 1, query);
    return resource
      ? NextResponse.json(resource, { headers: { 'Cache-Control': 'no-store' } })
      : NextResponse.json({ detail: 'Resource not found.' }, { status: 404 });
  } catch (error) {
    if (error instanceof ApiError) {
      return NextResponse.json({ detail: error.message }, { status: error.status });
    }

    return NextResponse.json({ detail: 'The resource could not be loaded.' }, { status: 500 });
  }
}
