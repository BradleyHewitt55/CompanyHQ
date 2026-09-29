import type { Metadata } from 'next';

import './globals.css';
import { Geist } from "next/font/google";
import { cn } from "@/lib/utils";
import { ThemeProvider } from '@/Components/theme';
import { getCompanyProfile } from '../src/api/company';

const geist = Geist({subsets:['latin'],variable:'--font-sans'});

export async function generateMetadata(): Promise<Metadata> {
  const company = await getCompanyProfile();
  return {
    title: {
      default: company?.name ?? '',
      template: company?.name ? `%s | ${company.name}` : '%s',
    },
    description: company?.description ?? '',
    robots: { index: false, follow: false },
    icons: { icon: company?.logoUrl ?? '/logo-frame3.svg' },
  };
}

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en" className={cn("font-sans", geist.variable)}>
      <body suppressHydrationWarning><ThemeProvider>{children}</ThemeProvider></body>
    </html>
  );
}
