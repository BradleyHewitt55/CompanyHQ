export { cn } from "cn"

/** Joins a person's first and last name for display, or an empty string when neither is set. */
export const fullName = (person?: { firstName?: string | null; lastName?: string | null } | null) =>
  [person?.firstName, person?.lastName].map((part) => part?.trim()).filter(Boolean).join(" ")
