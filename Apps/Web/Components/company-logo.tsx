import type { CSSProperties } from 'react';

export function CompanyLogo({
  alt,
  className,
  isSvg,
  src,
}: {
  alt: string;
  className: string;
  isSvg?: boolean;
  src: string;
}) {
  const tintableSvg = (isSvg ?? /\.svg(?:[?#]|$)/i.test(src)) && (src.startsWith('/') || src.startsWith('blob:'));
  if (!tintableSvg) return <img alt={alt} className={className} src={src} />;

  return (
    <span
      aria-hidden={!alt}
      aria-label={alt || undefined}
      className={`${className} company-logo company-logo--svg`}
      role={alt ? 'img' : undefined}
      style={{ '--company-logo-image': `url(${JSON.stringify(src)})` } as CSSProperties}
    />
  );
}
