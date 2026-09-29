'use client';

import { useEffect, useRef } from 'react';

const INFINITE_SCROLL_RETRY_BASE_MS = 250;
const INFINITE_SCROLL_RETRY_MAX_MS = 4000;

export function InfiniteScroll({ hasMore, loading, onLoadMore }: { hasMore: boolean; loading: boolean; onLoadMore: () => void }) {
  const sentinel = useRef<HTMLDivElement>(null);
  const load = useRef(onLoadMore);
  const lastTarget = useRef<Element | null>(null);
  const retryAttempt = useRef(0);
  load.current = onLoadMore;
  useEffect(() => {
    const element = sentinel.current;
    if (!hasMore) {
      lastTarget.current = null;
      retryAttempt.current = 0;
      return;
    }
    if (!element || loading) return;

    const target = findPaginationTarget(element);
    const observed = target ?? element;
    if (lastTarget.current !== observed) retryAttempt.current = 0;
    const delay = retryAttempt.current === 0
      ? 0
      : Math.min(INFINITE_SCROLL_RETRY_BASE_MS * 2 ** (retryAttempt.current - 1), INFINITE_SCROLL_RETRY_MAX_MS);
    let timer: ReturnType<typeof setTimeout> | undefined;
    let observer: IntersectionObserver | undefined;
    const observe = () => {
      observer = new IntersectionObserver((entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          observer?.unobserve(observed);
          lastTarget.current = observed;
          retryAttempt.current = Math.min(retryAttempt.current + 1, 5);
          load.current();
        }
      }, { rootMargin: '0px', threshold: 0 });
      observer.observe(observed);
    };
    if (delay > 0) timer = setTimeout(observe, delay);
    else observe();
    return () => {
      if (timer) clearTimeout(timer);
      observer?.disconnect();
    };
  }, [hasMore, loading]);
  return <div aria-hidden="true" className="infinite-scroll-trigger" ref={sentinel}>{loading && <span className="infinite-scroll-trigger__spinner" />}</div>;
}
function findPaginationTarget(trigger: HTMLElement): Element | null {
  let parent = trigger.parentElement;
  while (parent) {
    const lists = Array.from(parent.querySelectorAll<HTMLElement>('[data-infinite-scroll-list]'));
    const precedingLists = lists.filter((list) => {
      return (list.compareDocumentPosition(trigger) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
    });
    const rootLists = precedingLists.filter((list) => {
      let ancestor = list.parentElement;
      while (ancestor && ancestor !== parent) {
        if (precedingLists.includes(ancestor)) return false;
        ancestor = ancestor.parentElement;
      }
      return true;
    });
    const items = rootLists.flatMap((list) => Array.from(list.children));
    if (items.length > 0) return items[Math.max(0, items.length - 3)];
    parent = parent.parentElement;
  }
  return null;
}
