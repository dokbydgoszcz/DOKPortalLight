import { Attachment } from '../../shared/attachments/attachment.model';

/** Osoba po formacji w SKŚP, której nie udzielono jeszcze posługi. */
export interface PendingCatechist {
  candidateId: string;
  personId: string;
  personFullName: string;
  parishName: string | null;
  formationCompletedOn: string;
}

export interface Mission {
  id: string;
  personId: string;
  personFullName: string;
  servicePlace: string;
  missionStartDate: string;
  missionEndDate: string;
  grantedDate: string | null;
  grantedPlace: string | null;
  supervisionGroup: string | null;
  sentToDok: boolean;
  status: string;
  attachments: Attachment[];
}

export interface MissionFormValue {
  personId: string;
  servicePlace: string;
  missionStartDate: string;
  missionEndDate: string;
  grantedDate?: string;
  grantedPlace?: string;
  supervisionGroup?: string;
  sentToDok?: boolean;
}
