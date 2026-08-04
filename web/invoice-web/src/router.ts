import { createRouter, createWebHistory } from 'vue-router'
import { queryClient } from './query-client'
import { sessionQueryOptions } from './session'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('./features/auth/LoginView.vue'),
      meta: { public: true },
    },
    {
      path: '/',
      component: () => import('./layouts/AppShell.vue'),
      children: [
        {
          path: '',
          name: 'overview',
          component: () => import('./features/dashboard/OverviewView.vue'),
        },
        {
          path: 'delivery',
          name: 'delivery',
          component: () => import('./features/delivery/DeliveryProbeView.vue'),
        },
        {
          path: 'customers',
          name: 'customers',
          component: () => import('./features/customers/CustomerListView.vue'),
        },
        {
          path: 'customers/new',
          name: 'customer-create',
          component: () => import('./features/customers/CustomerCreateView.vue'),
        },
        {
          path: 'customers/:customerId',
          name: 'customer-detail',
          component: () => import('./features/customers/CustomerDetailView.vue'),
        },
        {
          path: 'customers/:customerId/edit',
          name: 'customer-edit',
          component: () => import('./features/customers/CustomerEditView.vue'),
        },
        {
          path: 'invoices',
          name: 'invoices',
          component: () => import('./features/invoices/InvoiceListView.vue'),
        },
        {
          path: 'invoices/new',
          name: 'invoice-create',
          component: () => import('./features/invoices/InvoiceCreateView.vue'),
        },
        {
          path: 'invoices/:invoiceId',
          name: 'invoice-detail',
          component: () => import('./features/invoices/InvoiceDetailView.vue'),
        },
        {
          path: 'invoices/:invoiceId/edit',
          name: 'invoice-edit',
          component: () => import('./features/invoices/InvoiceEditView.vue'),
        },
      ],
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('./features/NotFoundView.vue'),
      meta: { public: true },
    },
  ],
})

router.beforeEach(async (to) => {
  const session = await queryClient.ensureQueryData(sessionQueryOptions())
  if (!to.meta.public && !session) {
    return { name: 'login', query: { returnTo: to.fullPath } }
  }
  if (to.name === 'login' && session) return { name: 'overview' }
  return true
})
