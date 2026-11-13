export enum DiagramTypeEnum {
  BusinessContext = 1,
  UseCase = 2,
  Sequence = 3,
  Activity = 4,
  State = 5,
}

export interface DiagramTypeOption {
  value: number;
  label: string;
  badge: string;
  description: string;
  defaultTitle: string;
}

export const DIAGRAM_TYPE_OPTIONS: DiagramTypeOption[] = [
  {
    value: DiagramTypeEnum.BusinessContext,
    label: 'Business Context',
    badge: 'Context',
    description: 'System in the center, external entities around it and the data flows between them.',
    defaultTitle: 'System Context Diagram',
  },
  {
    value: DiagramTypeEnum.UseCase,
    label: 'Use Case',
    badge: 'Use Case',
    description: 'Actors, use cases and association / include / extend / generalization relations.',
    defaultTitle: 'Use Case Diagram',
  },
  {
    value: DiagramTypeEnum.Sequence,
    label: 'Sequence',
    badge: 'Sequence',
    description: 'Participants, request / response messages and alt / opt / loop fragments.',
    defaultTitle: 'Sequence Diagram',
  },
  {
    value: DiagramTypeEnum.Activity,
    label: 'Activity',
    badge: 'Activity',
    description: 'Workflows, sequential steps, decision branches, and terminal conditions.',
    defaultTitle: 'Activity Workflow Diagram',
  },
  {
    value: DiagramTypeEnum.State,
    label: 'State Machine',
    badge: 'State',
    description: 'Lifecycle states, initial state, triggers, and state transitions.',
    defaultTitle: 'State Lifecycle Diagram',
  },
];

export interface DiagramTemplate {
  // Khung JSON để điền vào chỗ trống <...>; "title" để trống vì tự đồng bộ từ ô Diagram Title
  skeleton: string;
  // Ví dụ đầy đủ để người dùng xem và làm theo
  example: { title: string; json: string };
  // Hướng dẫn ngắn cho từng trường
  guide: string[];
}

function toJson(model: object): string {
  return JSON.stringify(model, null, 2);
}

export const DIAGRAM_TEMPLATES: Record<number, DiagramTemplate> = {
  [DiagramTypeEnum.BusinessContext]: {
    skeleton: toJson({
      title: '',
      system: '<System name>',
      internalComponents: [],
      externalSystems: ['<External entity 1>', '<External entity 2>'],
      relations: [
        { from: '<External entity 1>', to: '<System name>', protocol: '<Data sent to the system>' },
        { from: '<System name>', to: '<External entity 1>', protocol: '<Data returned to the entity>' },
        { from: '<System name>', to: '<External entity 2>', protocol: '<Data sent to the entity>' },
      ],
    }),
    example: {
      title: 'CampusEats - Business Context',
      json: toJson({
        title: 'CampusEats - Business Context',
        system: 'CampusEats Platform',
        internalComponents: ['Student Web App', 'Canteen Portal', 'Backend API', 'Order Worker', 'SQL Server'],
        externalSystems: ['Student', 'Canteen Staff', 'VNPay', 'FPT Student SSO', 'Admin', 'Firebase Push Notification'],
        relations: [
          { from: 'Student', to: 'CampusEats Platform', protocol: 'Cart and order details' },
          { from: 'CampusEats Platform', to: 'Student', protocol: 'Order status and receipt' },
          { from: 'Canteen Staff', to: 'CampusEats Platform', protocol: 'Menu, stock and order confirmation' },
          { from: 'CampusEats Platform', to: 'Canteen Staff', protocol: 'New paid orders' },
          { from: 'CampusEats Platform', to: 'VNPay', protocol: 'Payment request' },
          { from: 'VNPay', to: 'CampusEats Platform', protocol: 'Payment result (IPN)' },
          { from: 'CampusEats Platform', to: 'FPT Student SSO', protocol: 'Login credentials' },
          { from: 'FPT Student SSO', to: 'CampusEats Platform', protocol: 'Student identity' },
          { from: 'Admin', to: 'CampusEats Platform', protocol: 'Canteen and account settings' },
          { from: 'CampusEats Platform', to: 'Admin', protocol: 'Sales report' },
          { from: 'Order Worker', to: 'Firebase Push Notification', protocol: 'Order-ready notification' },
        ],
      }),
    },
    guide: [
      '"system" is your system; it is drawn as the big circle in the middle.',
      '"externalSystems" lists the people and outside systems that exchange data with your system.',
      '"relations" holds one data flow per item: "from"/"to" must be the system name or a name from "externalSystems", "protocol" is the data that flows (e.g. "Payment request").',
      '"internalComponents" is optional: internal parts used in "from"/"to" are merged into the system circle.',
    ],
  },

  [DiagramTypeEnum.UseCase]: {
    skeleton: toJson({
      title: '',
      systemBoundary: '<System name>',
      actors: ['<Actor 1>', '<Actor 2>'],
      useCases: ['<Use case 1>', '<Use case 2>', '<Use case 3>'],
      relations: [
        { from: '<Actor 1>', to: '<Use case 1>', type: 'association' },
        { from: '<Actor 2>', to: '<Use case 2>', type: 'association' },
        { from: '<Use case 1>', to: '<Use case 3>', type: 'include' },
      ],
    }),
    example: {
      title: 'CampusEats - Use Cases',
      json: toJson({
        title: 'CampusEats - Use Cases',
        systemBoundary: 'CampusEats',
        actors: ['Student', 'Canteen Staff', 'Canteen Manager', 'VNPay'],
        useCases: [
          'Log in',
          'Browse menu',
          'Place order',
          'Apply voucher',
          'Pay online',
          'Pay with VNPay',
          'Track order status',
          'Confirm order',
          'Update menu and stock',
          'View sales report',
        ],
        relations: [
          { from: 'Student', to: 'Browse menu' },
          { from: 'Student', to: 'Place order' },
          { from: 'Student', to: 'Track order status' },
          { from: 'Place order', to: 'Log in', type: 'include' },
          { from: 'Place order', to: 'Pay online', type: 'include' },
          { from: 'Apply voucher', to: 'Place order', type: 'extend' },
          { from: 'Pay with VNPay', to: 'Pay online', type: 'generalization' },
          { from: 'Pay with VNPay', to: 'VNPay' },
          { from: 'Canteen Staff', to: 'Confirm order' },
          { from: 'Canteen Staff', to: 'Update menu and stock' },
          { from: 'Canteen Manager', to: 'Canteen Staff', type: 'generalization' },
          { from: 'Canteen Manager', to: 'View sales report' },
        ],
      }),
    },
    guide: [
      '"actors" are user roles or outside systems; "useCases" are short verb phrases such as "Place order".',
      'Every "from"/"to" must match a name in "actors" or "useCases" exactly.',
      '"type": "association" links an actor and a use case (default when omitted).',
      '"type": "include" goes from the base use case to the use case it always includes; "extend" goes from the optional use case to the base one.',
      '"type": "generalization" goes from the child to the parent (two actors or two use cases).',
    ],
  },

  [DiagramTypeEnum.Sequence]: {
    skeleton: toJson({
      title: '',
      participants: ['<Participant 1>', '<Participant 2>', '<Participant 3>'],
      messages: [
        { from: '<Participant 1>', to: '<Participant 2>', label: '<Request>', isResponse: false },
        { from: '<Participant 2>', to: '<Participant 3>', label: '<Request>', isResponse: false },
        { from: '<Participant 3>', to: '<Participant 2>', label: '<Response>', isResponse: true },
        {
          fragment: 'alt',
          branches: [
            {
              condition: '<Success condition>',
              messages: [{ from: '<Participant 2>', to: '<Participant 1>', label: '<Success result>', isResponse: true }],
            },
            {
              condition: '<Failure condition>',
              messages: [{ from: '<Participant 2>', to: '<Participant 1>', label: '<Error message>', isResponse: true }],
            },
          ],
        },
      ],
    }),
    example: {
      title: 'CampusEats - Place and Pay Order',
      json: toJson({
        title: 'CampusEats - Place and Pay Order',
        participants: ['Student', 'Web App', 'Backend API', 'SQL Server', 'VNPay', 'Order Worker'],
        messages: [
          { from: 'Student', to: 'Web App', label: 'Tap Checkout', isResponse: false },
          { from: 'Web App', to: 'Backend API', label: 'POST /api/orders', isResponse: false },
          {
            fragment: 'loop',
            branches: [
              {
                condition: 'for each dish in cart',
                messages: [
                  { from: 'Backend API', to: 'SQL Server', label: 'Check stock of dish', isResponse: false },
                  { from: 'SQL Server', to: 'Backend API', label: 'Stock quantity', isResponse: true },
                ],
              },
            ],
          },
          {
            fragment: 'alt',
            branches: [
              {
                condition: 'all dishes in stock',
                messages: [
                  { from: 'Backend API', to: 'SQL Server', label: 'Save order (Pending)', isResponse: false },
                  {
                    fragment: 'opt',
                    branches: [
                      {
                        condition: 'student entered a voucher',
                        messages: [
                          { from: 'Backend API', to: 'SQL Server', label: 'Validate voucher and apply discount', isResponse: false },
                          { from: 'SQL Server', to: 'Backend API', label: 'Discounted total', isResponse: true },
                        ],
                      },
                    ],
                  },
                  { from: 'Backend API', to: 'VNPay', label: 'Create payment URL', isResponse: false },
                  { from: 'VNPay', to: 'Backend API', label: 'Payment URL', isResponse: true },
                  { from: 'Backend API', to: 'Web App', label: '201 Created: orderId + paymentUrl', isResponse: true },
                  { from: 'Student', to: 'VNPay', label: 'Confirm payment', isResponse: false },
                  {
                    fragment: 'alt',
                    branches: [
                      {
                        condition: 'payment success',
                        messages: [
                          { from: 'VNPay', to: 'Backend API', label: 'IPN callback: success', isResponse: false },
                          { from: 'Backend API', to: 'Order Worker', label: 'Publish order.paid event', isResponse: false },
                          { from: 'Order Worker', to: 'Web App', label: 'Push: order sent to canteen', isResponse: true },
                        ],
                      },
                      {
                        condition: 'payment failed or timeout',
                        messages: [
                          { from: 'VNPay', to: 'Backend API', label: 'IPN callback: failed', isResponse: false },
                          { from: 'Backend API', to: 'Web App', label: 'Payment failed', isResponse: true },
                        ],
                      },
                    ],
                  },
                ],
              },
              {
                condition: 'some dishes sold out',
                messages: [{ from: 'Backend API', to: 'Web App', label: '409 Conflict: sold-out dishes', isResponse: true }],
              },
            ],
          },
        ],
      }),
    },
    guide: [
      '"participants" are drawn left to right in the order you list them.',
      'Each message needs "from", "to" (names from "participants") and a "label"; "isResponse": true draws a dashed return arrow.',
      'Add a block with "fragment": "alt" (if / else, 2 or more branches), "opt" (optional, 1 branch) or "loop" (repeat, 1 branch); blocks can be nested.',
    ],
  },

  [DiagramTypeEnum.Activity]: {
    skeleton: toJson({
      title: '',
      startNode: '<First action>',
      steps: ['<Step 1>', '<Step 2>', '<Step 3>'],
      decisions: [{ condition: '<Question?>', yesBranch: '<Step 2>', noBranch: '<Action when the answer is no>' }],
    }),
    example: {
      title: 'CampusEats - Order a Meal',
      json: toJson({
        title: 'CampusEats - Order a Meal',
        startNode: 'Open CampusEats',
        steps: ['Choose canteen', 'Add dishes to cart', 'Place order', 'Pay with VNPay', 'Wait for order ready', 'Pick up meal at counter'],
        decisions: [
          { condition: 'Logged in?', yesBranch: 'Choose canteen', noBranch: 'Log in with student account' },
          { condition: 'All dishes in stock?', yesBranch: 'Place order', noBranch: 'Remove sold-out dishes' },
          { condition: 'Payment successful?', yesBranch: 'Wait for order ready', noBranch: 'Cancel order after 10 minutes' },
        ],
      }),
    },
    guide: [
      '"startNode" is the first action after the start dot; "steps" are the next actions in order.',
      'A decision whose "yesBranch" equals one of the "steps" is drawn at that step; "noBranch" is the alternative action.',
    ],
  },

  [DiagramTypeEnum.State]: {
    skeleton: toJson({
      title: '',
      initialState: '<State 1>',
      states: ['<State 1>', '<State 2>', '<State 3>'],
      transitions: [
        { from: '<State 1>', event: '<Event>', to: '<State 2>' },
        { from: '<State 2>', event: '<Event>', to: '<State 3>' },
      ],
    }),
    example: {
      title: 'CampusEats - Order Lifecycle',
      json: toJson({
        title: 'CampusEats - Order Lifecycle',
        initialState: 'Pending',
        states: ['Pending', 'Paid', 'Confirmed', 'Preparing', 'Ready', 'Completed', 'Cancelled'],
        transitions: [
          { from: 'Pending', event: 'VNPay callback: success', to: 'Paid' },
          { from: 'Pending', event: 'not paid within 10 minutes', to: 'Cancelled' },
          { from: 'Paid', event: 'staff confirms order', to: 'Confirmed' },
          { from: 'Paid', event: 'staff rejects (sold out) + refund', to: 'Cancelled' },
          { from: 'Confirmed', event: 'kitchen starts cooking', to: 'Preparing' },
          { from: 'Preparing', event: 'staff marks ready', to: 'Ready' },
          { from: 'Ready', event: 'student picks up', to: 'Completed' },
        ],
      }),
    },
    guide: [
      '"initialState" must be one of "states".',
      'Each transition goes "from" one state "to" another when "event" happens; states with no outgoing transition become final states.',
    ],
  },
};

export function getDiagramTemplate(type: number): DiagramTemplate {
  return DIAGRAM_TEMPLATES[type] ?? DIAGRAM_TEMPLATES[DiagramTypeEnum.BusinessContext];
}
