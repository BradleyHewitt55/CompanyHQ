import {
  Activity,
  ArrowDown,
  ArrowDownToLine,
  ArrowLeft,
  ArrowUp,
  Ban,
  Bell,
  BookHeart,
  Building2,
  Bookmark,
  Camera,
  Check,
  ChevronDown,
  ChevronRight,
  CircleCheck,
  CircleHelp,
  Clock,
  Compass,
  EllipsisVertical,
  ExternalLink,
  EyeOff,
  FileText,
  Film,
  Flag,
  Heart,
  House,
  Images,
  KeyRound,
  Languages,
  Laptop,
  LayoutDashboard,
  ListFilter,
  LogOut,
  Mail,
  MailWarning,
  Map,
  MapPin,
  Menu,
  Megaphone,
  MessageCircle,
  MessageSquareText,
  MessagesSquare,
  Newspaper,
  OctagonAlert,
  Paperclip,
  Pencil,
  Play,
  Plus,
  Search,
  Send,
  Settings,
  Share2,
  ShieldLock,
  Star,
  Stethoscope,
  Trash2,
  UserRound,
  UserRoundX,
  Users,
  Video,
  WifiOff,
  X,
  type LucideIcon,
} from 'lucide-react';

export type IconName =
  | 'organization'
  | 'activity'
  | 'article'
  | 'arrow-left'
  | 'arrow-down'
  | 'arrow-up'
  | 'blog'
  | 'check'
  | 'chevron-down'
  | 'chevron-right'
  | 'clock'
  | 'close'
  | 'comment'
  | 'dashboard'
  | 'edit'
  | 'external'
  | 'faq'
  | 'filter'
  | 'flag'
  | 'gallery'
  | 'heart'
  | 'heart-filled'
  | 'help'
  | 'home'
  | 'key'
  | 'language'
  | 'logout'
  | 'map'
  | 'menu'
  | 'more-vertical'
  | 'messages'
  | 'notifications'
  | 'plus'
  | 'posts'
  | 'person'
  | 'paperclip'
  | 'search'
  | 'send'
  | 'settings'
  | 'share'
  | 'users'
  | 'block'
  | 'bookmark'
  | 'bookmark-fill'
  | 'bookmark-heart'
  | 'box-arrow-in-down'
  | 'camera'
  | 'camera-video'
  | 'chat-square-text'
  | 'check2-circle'
  | 'compass'
  | 'envelope'
  | 'envelope-exclamation'
  | 'exclamation-octagon'
  | 'eye-slash'
  | 'film'
  | 'geo-alt'
  | 'images'
  | 'laptop'
  | 'megaphone'
  | 'person-slash'
  | 'play-fill'
  | 'question-circle'
  | 'shield-lock'
  | 'stethoscope'
  | 'star'
  | 'star-filled'
  | 'three-dots'
  | 'trash3'
  | 'wifi-off';

type IconProps = {
  name: IconName;
  size?: number;
  className?: string;
};

const iconMap: Record<IconName, LucideIcon> = {
  activity: Activity,
  article: FileText,
  'arrow-left': ArrowLeft,
  'arrow-down': ArrowDown,
  'arrow-up': ArrowUp,
  blog: Newspaper,
  check: Check,
  'chevron-down': ChevronDown,
  'chevron-right': ChevronRight,
  clock: Clock,
  close: X,
  comment: MessageCircle,
  dashboard: LayoutDashboard,
  edit: Pencil,
  external: ExternalLink,
  faq: CircleHelp,
  filter: ListFilter,
  flag: Flag,
  gallery: Images,
  heart: Heart,
  'heart-filled': Heart,
  help: CircleHelp,
  home: House,
  key: KeyRound,
  language: Languages,
  logout: LogOut,
  map: Map,
  menu: Menu,
  'more-vertical': EllipsisVertical,
  messages: MessagesSquare,
  notifications: Bell,
  plus: Plus,
  posts: MessageSquareText,
  person: UserRound,
  paperclip: Paperclip,
  search: Search,
  send: Send,
  settings: Settings,
  share: Share2,
  users: Users,
  organization: Building2,
  block: Ban,
  bookmark: Bookmark,
  'bookmark-fill': Bookmark,
  'bookmark-heart': BookHeart,
  'box-arrow-in-down': ArrowDownToLine,
  camera: Camera,
  'camera-video': Video,
  'chat-square-text': MessageSquareText,
  'check2-circle': CircleCheck,
  compass: Compass,
  envelope: Mail,
  'envelope-exclamation': MailWarning,
  'exclamation-octagon': OctagonAlert,
  'eye-slash': EyeOff,
  film: Film,
  'geo-alt': MapPin,
  images: Images,
  laptop: Laptop,
  megaphone: Megaphone,
  'person-slash': UserRoundX,
  'play-fill': Play,
  'question-circle': CircleHelp,
  'shield-lock': ShieldLock,
  star: Star,
  'star-filled': Star,
  stethoscope: Stethoscope,
  'three-dots': EllipsisVertical,
  trash3: Trash2,
  'wifi-off': WifiOff,
};

const filledIcons = new Set<IconName>(['bookmark-fill', 'heart-filled', 'play-fill', 'star-filled']);

export function Icon({ name, size = 18, className }: IconProps) {
  const Lucide = iconMap[name];
  return <Lucide
    aria-hidden="true"
    className={className}
    fill={filledIcons.has(name) ? 'currentColor' : 'none'}
    focusable="false"
    height={size}
    strokeLinecap="round"
    strokeLinejoin="round"
    strokeWidth={1.8}
    width={size}
  />;
}

