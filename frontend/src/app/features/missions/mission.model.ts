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
  status: string;
}

export interface MissionFormValue {
  personId: string;
  servicePlace: string;
  missionStartDate: string;
  missionEndDate: string;
  grantedDate?: string;
  grantedPlace?: string;
  supervisionGroup?: string;
}
