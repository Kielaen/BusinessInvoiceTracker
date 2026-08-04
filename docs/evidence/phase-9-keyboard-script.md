# Phase 9 Manual Keyboard Script

Status: **PASSED**  
Prepared: 2026-08-03 (Africa/Johannesburg)  
Completed: 2026-08-03 (Africa/Johannesburg)  
Target: `http://127.0.0.1:8080/`

This is the remaining Phase 9 gate. Do not mark it passed from DOM structure,
axe results or automated focus assertions alone.

## Preconditions

- `docker compose --env-file .env.example up --detach --wait` is healthy.
- Use a desktop browser with no mouse, touch or screen interaction after placing
  focus in the page.
- Sign in with the documented local review Owner account.

## Script

1. From the sign-in page, use `Tab` and `Shift+Tab` to reach Email address,
   Password and Sign in in logical order. Confirm every focused item has a visible
   focus indicator. Press `Enter` on Sign in and confirm the Overview loads.
2. Press `Tab` from the top of Overview. Confirm Skip to main content is the first
   page control, activate it with `Enter`, and confirm focus moves to the main
   content rather than traversing the entire header.
3. Continue with `Tab`/`Shift+Tab` through the business selector, Sign out,
   primary navigation, Review invoices, View all, overdue invoice links and recent
   activity links. Confirm focus remains visible and order follows the visual flow.
4. Activate the Invoices navigation link with `Enter`. Confirm the Invoices page
   loads and focus is not trapped.
5. Reach Search invoices, type a known invoice/customer fragment, wait for the
   result announcement, and confirm the displayed count/list changes. Clear the
   text with the keyboard.
6. Reach Status, choose Overdue with arrow keys, and confirm the result list and
   announcement update. Reach Sort by and Direction, change both, and confirm the
   order updates. Activate Clear filters with `Enter` or `Space` and confirm the
   defaults return.
7. Reach Next, activate it with `Enter`, confirm Page 2 is announced/displayed,
   then activate Previous. Confirm disabled pagination controls are skipped or
   cannot be activated.
8. Use `Shift+Tab` back through the page. Confirm there is no keyboard trap and
   the order is the reverse of forward navigation.

## Pass criteria

- Every required control is keyboard reachable and performs its visible action.
- Focus is always visible and follows a logical forward/reverse order.
- Skip link works; no focus trap exists.
- Filters, sorting, clearing and paging visibly update the real server-backed UI.
- Result-count/status changes are announced by the existing status region.

## Evidence already passed

- V-1002: 26 frontend tests, including axe and rendered control/state behavior.
- V-1002: live nginx dashboard/invoice journey and 375px responsive check.
- V-1004: fresh-directory/fresh-volume full verification.

## Current blocker

The supported in-app browser session rendered and inspected the real UI, but its
native keyboard injection did not move focus or activate controls. This is a test
harness limitation, not evidence of either application success or failure. Record
the operator, browser and result below after a real keyboard-only run.

Operator: **User (Kiela)**  
Browser/version: **User-run browser; version was not captured**  
Date/time: **2026-08-03; exact time was not captured**  
Result: **PASS**  
Notes: **User explicitly confirmed: “Phase 9 keyboard passed.”**
